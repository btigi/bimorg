using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Bimorg.ViewModels;

namespace Bimorg.Views;

public partial class KeywordsBrowseWindow : Window
{
    private readonly MainViewModel _mainVm;

    public KeywordsBrowseWindow(MainViewModel mainVm, IReadOnlyList<string> keywords, string subtitle)
    {
        _mainVm = mainVm;
        InitializeComponent();
        ThemeManager.InitializeWindow(this);
        Title = "Keywords · " + subtitle;
        KeywordsList.ItemsSource = keywords;
    }

    private void KeywordsList_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is not ListBox lb)
            return;

        var hit = e.OriginalSource as DependencyObject;
        if (hit is null)
            return;

        var container = ItemsControl.ContainerFromElement(lb, hit);
        if (container is ListBoxItem lbi && lbi.Content is string word)
            _mainVm.AppendKeywordToSearch(word);
    }
}