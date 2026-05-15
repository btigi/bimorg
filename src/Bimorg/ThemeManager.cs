using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Microsoft.Win32;

namespace Bimorg;

public static class ThemeManager
{
    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    private const int DwmwaUseImmersiveDarkMode = 20;

    private static readonly Uri DarkThemeUri = new("pack://application:,,,/MahApps.Metro;component/Styles/Themes/Dark.Steel.xaml");
    private static readonly Uri LightThemeUri = new("pack://application:,,,/MahApps.Metro;component/Styles/Themes/Light.Steel.xaml");

    private static readonly string PreferencePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Bimorg", "theme.preference");

    public static ThemePreference CurrentPreference { get; private set; }

    public static bool IsDarkTheme { get; private set; }

    public static bool IsSystemDarkMode()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            var v = key?.GetValue("AppsUseLightTheme");
            return v is int i && i == 0;
        }
        catch
        {
            return false;
        }
    }

    public static ThemePreference LoadThemePreference()
    {
        try
        {
            if (!File.Exists(PreferencePath))
            {
                CurrentPreference = ThemePreference.System;
                return CurrentPreference;
            }

            var text = File.ReadAllText(PreferencePath).Trim().ToLowerInvariant();
            CurrentPreference = text switch
            {
                "dark" or "1" or "true" => ThemePreference.Dark,
                "light" or "0" or "false" => ThemePreference.Light,
                "system" => ThemePreference.System,
                _ => ThemePreference.System,
            };
        }
        catch
        {
            CurrentPreference = ThemePreference.System;
        }

        return CurrentPreference;
    }

    private static void SaveThemePreference(ThemePreference preference)
    {
        try
        {
            var dir = Path.GetDirectoryName(PreferencePath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            var text = preference switch
            {
                ThemePreference.Dark => "dark",
                ThemePreference.Light => "light",
                _ => "system",
            };
            File.WriteAllText(PreferencePath, text);
        }
        catch
        {
            // :(
        }
    }

    public static bool ResolveIsDark(ThemePreference preference) => preference switch
    {
        ThemePreference.Dark => true,
        ThemePreference.Light => false,
        _ => IsSystemDarkMode(),
    };

    public static void ApplySavedThemePreference()
    {
        LoadThemePreference();
        ApplyVisualTheme(ResolveIsDark(CurrentPreference));
    }

    public static void ApplyThemePreference(ThemePreference preference)
    {
        CurrentPreference = preference;
        SaveThemePreference(preference);
        ApplyVisualTheme(ResolveIsDark(preference));
    }

    private static void ApplyVisualTheme(bool useDark)
    {
        IsDarkTheme = useDark;

        var app = System.Windows.Application.Current;
        if (app?.Resources.MergedDictionaries is null)
            return;

        ResourceDictionary? existingTheme = null;
        foreach (var dict in app.Resources.MergedDictionaries)
        {
            if (dict.Source is not null && dict.Source.ToString().Contains("/Themes/", StringComparison.Ordinal))
            {
                existingTheme = dict;
                break;
            }
        }

        var newThemeUri = useDark ? DarkThemeUri : LightThemeUri;
        var newTheme = new ResourceDictionary { Source = newThemeUri };

        if (existingTheme is not null)
        {
            var index = app.Resources.MergedDictionaries.IndexOf(existingTheme);
            app.Resources.MergedDictionaries[index] = newTheme;
        }
        else
        {
            app.Resources.MergedDictionaries.Add(newTheme);
        }

        foreach (System.Windows.Window window in app.Windows)
            UpdateWindowTitleBar(window, useDark);
    }

    public static void UpdateWindowTitleBar(System.Windows.Window window, bool useDark)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero)
            return;

        int value = useDark ? 1 : 0;
        _ = DwmSetWindowAttribute(hwnd, DwmwaUseImmersiveDarkMode, ref value, sizeof(int));
    }

    public static void InitializeWindow(System.Windows.Window window)
    {
        window.SourceInitialized += (_, _) => UpdateWindowTitleBar(window, IsDarkTheme);
    }
}