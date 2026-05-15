using System.Collections.Immutable;
using System.IO;
using Bimorg.Data;
using Bimorg.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Bimorg.Scan.Services;

public sealed class IndexingService(
    IDbContextFactory<AppDbContext> dbFactory,
    OllamaService ollama,
    ImageService images,
    IConfiguration configuration)
{
    private static readonly string[] DefaultAllowedExtensions =
    [
        ".jpg", ".jpeg", ".png", ".webp", ".bmp",
    ];

    private readonly ImmutableHashSet<string> _allowedExtensions =
        ParseAllowedExtensions(configuration);

    public async Task IndexDirectoryAsync(
        string directoryPath,
        IProgress<IndexProgress>? progress,
        CancellationToken cancellationToken)
    {
        directoryPath = Path.GetFullPath(directoryPath);
        if (!Directory.Exists(directoryPath))
            throw new DirectoryNotFoundException(directoryPath);

        var thumbSize = configuration.GetValue("ThumbnailSize", 200);

        var files = Directory.EnumerateFiles(directoryPath, "*", SearchOption.AllDirectories)
            .Where(IsImage)
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static f => f, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var total = files.Count;
        var index = 0;

        foreach (var path in files)
        {
            cancellationToken.ThrowIfCancellationRequested();

            index++;
            var name = Path.GetFileName(path);
            progress?.Report(new IndexProgress(index, total, name, $"Checking {name}"));

            await using var dbCheck = await dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
            var exists = await dbCheck.BattleMaps
                .AnyAsync(x => x.FilePath == path, cancellationToken)
                .ConfigureAwait(false);

            if (exists)
                continue;

            ImageFileInfo info;
            byte[] thumbnail;
            try
            {
                progress?.Report(new IndexProgress(index, total, name, $"Reading dimensions {name}"));
                info = images.ReadInfo(path);

                progress?.Report(new IndexProgress(index, total, name, $"Thumbnail {name}"));
                thumbnail = images.CreateThumbnail(path, thumbSize);
            }
            catch (Exception ex)
            {
                progress?.Report(new IndexProgress(index, total, name, $"Skip (read error): {name} — {ex.Message}"));
                continue;
            }

            DescribeImageResult described;
            try
            {
                progress?.Report(new IndexProgress(index, total, name, $"Vision AI {name}"));
                await using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                var imageBytes = await ReadAllBytesOptimizedAsync(fs, cancellationToken).ConfigureAwait(false);

                described = await ollama.DescribeImageAsync(imageBytes, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                progress?.Report(new IndexProgress(index, total, name, $"Skip (AI error): {name} — {ex.Message}"));
                continue;
            }

            progress?.Report(new IndexProgress(index, total, name, $"Saving {name}"));

            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
            var dup = await db.BattleMaps.AnyAsync(x => x.FilePath == path, cancellationToken).ConfigureAwait(false);
            if (dup)
                continue;

            var entity = new BattleMap
            {
                FileName = name,
                FilePath = path,
                Description = described.Description,
                ThumbnailData = thumbnail,
                Width = info.Width,
                Height = info.Height,
                FileSizeBytes = info.FileSizeBytes,
                DateAdded = DateTimeOffset.UtcNow,
                Keywords = described.Keywords
                    .Select(w => new MapKeyword { Word = w })
                    .ToList(),
            };

            db.BattleMaps.Add(entity);

            try
            {
                await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (DbUpdateException ex)
            {
                progress?.Report(new IndexProgress(index, total, name, $"Skip (DB): {name} — {ex.InnerException?.Message ?? ex.Message}"));
            }
        }
    }

    private static async Task<byte[]> ReadAllBytesOptimizedAsync(FileStream fs, CancellationToken ct)
    {
        if (fs.Length > int.MaxValue)
            throw new InvalidOperationException("Image file exceeds supported size.");

        await using var ms = new MemoryStream((int)Math.Min(fs.Length, int.MaxValue));
        await fs.CopyToAsync(ms, ct).ConfigureAwait(false);
        return ms.ToArray();
    }

    private bool IsImage(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return _allowedExtensions.Contains(ext);
    }

    private static ImmutableHashSet<string> ParseAllowedExtensions(IConfiguration cfg)
    {
        var fromConfig = cfg.GetSection("AllowedExtensions").Get<string[]>();
        IEnumerable<string> source = fromConfig is { Length: > 0 }
            ? fromConfig
            : DefaultAllowedExtensions;

        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var raw in source)
        {
            if (string.IsNullOrWhiteSpace(raw))
                continue;

            var e = raw.Trim().ToLowerInvariant();
            if (e.Length == 0)
                continue;
            if (e[0] != '.')
                e = '.' + e;
            set.Add(e);
        }

        if (set.Count == 0)
        {
            foreach (var ext in DefaultAllowedExtensions)
                set.Add(ext);
        }

        return ImmutableHashSet.CreateRange(StringComparer.OrdinalIgnoreCase, set);
    }
}

public readonly record struct IndexProgress(int Current, int Total, string CurrentFileName, string StatusLine);
