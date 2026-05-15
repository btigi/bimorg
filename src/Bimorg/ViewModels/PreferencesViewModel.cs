using CommunityToolkit.Mvvm.ComponentModel;

namespace Bimorg.ViewModels;

public sealed class ThemeOption(ThemePreference value, string label)
{
    public ThemePreference Value { get; } = value;
    public string Label { get; } = label;
}

public partial class PreferencesViewModel : ObservableObject
{
    public IReadOnlyList<ThemeOption> ThemeOptions { get; } =
    [
        new(ThemePreference.Light, "Light mode"),
        new(ThemePreference.Dark, "Dark mode"),
        new(ThemePreference.System, "Match system"),
    ];

    [ObservableProperty]
    private ThemePreference _selectedTheme;

    public PreferencesViewModel()
    {
        _selectedTheme = ThemeManager.CurrentPreference;
    }
}