using System.CommandLine;
using System.IO;
using Bimorg.Data;
using Bimorg.Scan.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Bimorg.Scan;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
            .Build();

        var connectionString = SqliteConnectionHelper.BuildConnectionString(configuration["DbPath"]);

        using var services = BuildServiceProvider(configuration, connectionString);
        await EnsureDatabaseCreatedAsync(services);

        var scanCmd = new Command("scan", "Index images under a directory (recursive).");
        var scanDir = new Option<string>(["--directory", "-d"], "Directory path to scan.") { IsRequired = true };
        scanCmd.Add(scanDir);
        scanCmd.SetHandler(async (string dir) =>
        {
            var svc = services.GetRequiredService<IndexingService>();
            var progress = new Progress<IndexProgress>(p =>
                Console.WriteLine($"{p.Current}/{p.Total}\t{p.CurrentFileName}\t{p.StatusLine}"));
            await svc.IndexDirectoryAsync(dir, progress, CancellationToken.None).ConfigureAwait(false);
        }, scanDir);

        var removeDirCmd = new Command("remove-directory", "Remove all indexed maps whose files live under the given directory.");
        var removeDirArg = new Option<string>(["--directory", "-d"], "Directory root path.") { IsRequired = true };
        removeDirCmd.Add(removeDirArg);
        removeDirCmd.SetHandler(async (string dir) =>
        {
            await RemoveDirectoryAsync(services.GetRequiredService<IDbContextFactory<AppDbContext>>(), dir)
                .ConfigureAwait(false);
        }, removeDirArg);

        var removeFileCmd = new Command("remove-file", "Remove the map entry for a single image file.");
        var removeFileArg = new Option<string>(["--path", "-p"], "Full path to the image file.") { IsRequired = true };
        removeFileCmd.Add(removeFileArg);
        removeFileCmd.SetHandler(async (string path) =>
        {
            await RemoveFileAsync(services.GetRequiredService<IDbContextFactory<AppDbContext>>(), path)
                .ConfigureAwait(false);
        }, removeFileArg);

        var root = new RootCommand("Manage the Bimorg SQLite image index.");
        root.AddCommand(scanCmd);
        root.AddCommand(removeDirCmd);
        root.AddCommand(removeFileCmd);

        try
        {
            return await root.InvokeAsync(args).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }

    private static ServiceProvider BuildServiceProvider(IConfiguration configuration, string connectionString)
    {
        var sc = new ServiceCollection();
        sc.AddSingleton<IConfiguration>(configuration);
        sc.AddDbContextFactory<AppDbContext>(o => o.UseSqlite(connectionString));
        sc.AddSingleton<OllamaService>();
        sc.AddSingleton<ImageService>();
        sc.AddSingleton<IndexingService>();
        return sc.BuildServiceProvider();
    }

    private static async Task EnsureDatabaseCreatedAsync(ServiceProvider services)
    {
        var factory = services.GetRequiredService<IDbContextFactory<AppDbContext>>();
        await using var db = await factory.CreateDbContextAsync().ConfigureAwait(false);
        await db.Database.EnsureCreatedAsync().ConfigureAwait(false);
    }

    private static async Task RemoveDirectoryAsync(IDbContextFactory<AppDbContext> factory, string directoryPath)
    {
        directoryPath = Path.GetFullPath(directoryPath);
        if (!Directory.Exists(directoryPath))
            throw new DirectoryNotFoundException(directoryPath);

        var rootFull = directoryPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var prefix = rootFull + Path.DirectorySeparatorChar;

        await using var db = await factory.CreateDbContextAsync().ConfigureAwait(false);

        var prefixLower = prefix.ToLowerInvariant();

        var ids = await db.BattleMaps.AsNoTracking()
            .Where(m => m.FilePath.ToLower().StartsWith(prefixLower))
            .Select(m => m.Id)
            .ToListAsync()
            .ConfigureAwait(false);

        if (ids.Count == 0)
        {
            Console.WriteLine("No entries found under that directory.");
            return;
        }

        await db.BattleMaps.Where(m => ids.Contains(m.Id)).ExecuteDeleteAsync().ConfigureAwait(false);
        Console.WriteLine($"Removed {ids.Count} entries.");
    }

    private static async Task RemoveFileAsync(IDbContextFactory<AppDbContext> factory, string filePath)
    {
        filePath = Path.GetFullPath(filePath);

        await using var db = await factory.CreateDbContextAsync().ConfigureAwait(false);

        var targetKey = filePath.ToLowerInvariant();

        var deleted = await db.BattleMaps
            .Where(m => m.FilePath.ToLower() == targetKey)
            .ExecuteDeleteAsync()
            .ConfigureAwait(false);

        if (deleted == 0)
        {
            Console.WriteLine("No entry matches that path.");
            return;
        }

        Console.WriteLine("Removed 1 entry.");
    }
}