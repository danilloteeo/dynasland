using System.Windows;
using System.Windows.Media;
using Dynasland.Core.Interfaces;
using Dynasland.Core.Models;
using Microsoft.Win32;

namespace Dynasland.App.Services;

/// <summary>
/// Aplica tema (claro/escuro/sistema), cor de destaque e aparência da ilha substituindo os pincéis
/// nos recursos da aplicação; tudo que usa DynamicResource é atualizado na hora.
/// </summary>
public sealed class ThemeService(IIslandSettingsService settings) : IDisposable
{
    private const string PersonalizeKey = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

    private static readonly Dictionary<string, (string Dark, string Light)> Palette = new()
    {
        ["SettingsBackgroundBrush"] = ("#202020", "#F3F3F3"),
        ["SettingsSidebarBrush"] = ("#1B1B1B", "#EBEBEB"),
        ["SettingsCardBrush"] = ("#2B2B2B", "#FFFFFF"),
        ["SettingsCardHoverBrush"] = ("#323232", "#F7F7F7"),
        ["SettingsBorderBrush"] = ("#3A3A3A", "#E2E2E2"),
        ["SettingsTextBrush"] = ("#F3F3F3", "#1B1B1B"),
        ["SettingsSecondaryTextBrush"] = ("#B8B8B8", "#5D5D5D"),
        ["SettingsTertiaryTextBrush"] = ("#8A8A8A", "#8A8A8A"),
        ["SettingsNavHoverBrush"] = ("#2D2D2D", "#E0E0E0"),
        ["SettingsButtonBrush"] = ("#373737", "#FBFBFB"),
        ["SettingsButtonHoverBrush"] = ("#404040", "#F0F0F0"),
        ["SettingsButtonBorderBrush"] = ("#444444", "#D5D5D5"),
        ["SettingsChipBrush"] = ("#333333", "#EFEFEF"),
        ["SettingsTrackBrush"] = ("#555555", "#C8C8C8"),
        ["SettingsScrollThumbBrush"] = ("#5A5A5A", "#BDBDBD"),
    };

    private Application? _application;

    public bool IsDark { get; private set; } = true;

    /// <summary>Disparado na thread de UI após aplicar um tema.</summary>
    public event EventHandler? ThemeChanged;

    public void Initialize(Application application)
    {
        _application = application;
        Apply(settings.Current);
        settings.Changed += OnSettingsChanged;
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
    }

    public static bool WindowsAppsUseLightTheme()
    {
        using var key = Registry.CurrentUser.OpenSubKey(PersonalizeKey);
        return key?.GetValue("AppsUseLightTheme") is int value && value == 1;
    }

    /// <summary>Cor do texto sobre a cor de destaque: escura em cores claras, branca nas escuras.</summary>
    public static Color ContrastOn(Color color)
    {
        var luminance = (0.2126 * color.R + 0.7152 * color.G + 0.0722 * color.B) / 255;
        return luminance > 0.6 ? Color.FromRgb(0x1B, 0x1B, 0x1B) : Colors.White;
    }

    private void Apply(IslandSettings current)
    {
        if (_application is null)
        {
            return;
        }

        IsDark = current.Theme switch
        {
            AppTheme.Dark => true,
            AppTheme.Light => false,
            _ => !WindowsAppsUseLightTheme(),
        };

        var resources = _application.Resources;
        foreach (var (key, (dark, light)) in Palette)
        {
            resources[key] = Brush(Parse(IsDark ? dark : light));
        }

        var accent = Parse(current.AccentColor);
        resources["SettingsAccentBrush"] = Brush(accent);
        resources["SettingsAccentTextBrush"] = Brush(ContrastOn(accent));
        resources["IslandAccentBrush"] = Brush(accent);

        // O fundo vem do tema da ilha (sempre escuro); a transparência só altera o alfa.
        resources["IslandBackgroundBrush"] = IslandThemes.Background(current.IslandTheme, accent, current.IslandOpacity);
        resources["IslandShadowOpacity"] = 0.7 * current.ShadowIntensity;

        ThemeChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnSettingsChanged(object? sender, IslandSettings current) =>
        _application?.Dispatcher.BeginInvoke(() => Apply(current));

    private void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        if (e.Category == UserPreferenceCategory.General && settings.Current.Theme == AppTheme.System)
        {
            _application?.Dispatcher.BeginInvoke(() => Apply(settings.Current));
        }
    }

    private static Color Parse(string hex) => (Color)ColorConverter.ConvertFromString(hex);

    private static SolidColorBrush Brush(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    public void Dispose()
    {
        settings.Changed -= OnSettingsChanged;
        SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
    }
}
