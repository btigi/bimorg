using System.Windows;
using Bimorg;
using Bimorg.ViewModels;

namespace Bimorg.Views;

public partial class PreferencesWindow : Window
{
    public PreferencesWindow()
    {
        InitializeComponent();
        DataContext = new PreferencesViewModel();
        ThemeManager.InitializeWindow(this);
    }

    private void Ok_OnClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is PreferencesViewModel vm)
            ThemeManager.ApplyThemePreference(vm.SelectedTheme);

        DialogResult = true;
    }
}