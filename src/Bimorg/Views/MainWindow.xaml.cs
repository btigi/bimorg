using System.Windows;
using System.Windows.Input;
using Bimorg.ViewModels;

namespace Bimorg.Views;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm;

    public MainWindow(MainViewModel vm)
    {
        InitializeComponent();
        DataContext = _vm = vm;
        ThemeManager.InitializeWindow(this);
    }

    private void Exit_OnClick(object sender, RoutedEventArgs e) =>
        System.Windows.Application.Current.Shutdown();

    private void Preferences_OnClick(object sender, RoutedEventArgs e)
    {
        var dlg = new PreferencesWindow { Owner = this };
        dlg.ShowDialog();
    }

    private async void MainWindow_OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            await _vm.RefreshSearchCommand.ExecuteAsync(null);
        }
        catch (Exception ex)
        {
            _vm.StatusLine = "Initial load failed: " + ex.Message;
        }
    }

    private void Thumbnail_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement el && el.DataContext is MapTileVm tile)
            _vm.OpenMapCommand.Execute(tile);
    }
}