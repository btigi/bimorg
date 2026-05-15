using System.Windows;
using Bimorg.Data;
using Bimorg.Services;
using Bimorg.ViewModels;
using Bimorg.Views;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Bimorg;

public partial class App : System.Windows.Application
{
    private ServiceProvider? _services;

    protected override async void OnStartup(System.Windows.StartupEventArgs e)
    {
        base.OnStartup(e);

        ThemeManager.ApplySavedThemePreference();

        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();

        var connectionString = SqliteConnectionHelper.BuildConnectionString(configuration["DbPath"]);

        var sc = new ServiceCollection();
        sc.AddSingleton<IConfiguration>(configuration);
        sc.AddDbContextFactory<AppDbContext>(o => o.UseSqlite(connectionString));
        sc.AddSingleton<MapsSearchService>();
        sc.AddSingleton<MainViewModel>(sp =>
            new MainViewModel(
                sp.GetRequiredService<MapsSearchService>(),
                configuration,
                Dispatcher));

        _services = sc.BuildServiceProvider();

        await EnsureDatabaseCreatedAsync(_services);

        var mw = new MainWindow(_services.GetRequiredService<MainViewModel>());
        MainWindow = mw;
        mw.Show();
    }

    private static async Task EnsureDatabaseCreatedAsync(ServiceProvider provider)
    {
        var factory = provider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        await using var db = await factory.CreateDbContextAsync();
        await db.Database.EnsureCreatedAsync();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _services?.Dispose();
        base.OnExit(e);
    }
}
