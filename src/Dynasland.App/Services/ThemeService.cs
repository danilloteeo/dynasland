using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Dynasland.Core.Interfaces;
using Dynasland.Core.Models;
using Dynasland.Core.Modules;
using Microsoft.Win32;

namespace Dynasland.App.Services;

/// <summary>
/// Aplica tema (claro/escuro/sistema), cor de destaque e aparência da ilha substituindo os pincéis
/// nos recursos da aplicação; tudo que usa DynamicResource é atualizado na hora.
/// </summary>
public sealed class ThemeService(IIslandSettingsService settings, IIslandTint tint) : IDisposable
{
    private static readonly Duration IslandColorFade = TimeSpan.FromMilliseconds(600);

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

    /// <summary>
    /// Pincel mutável do fundo da ilha: trocar de tema ou de música anima as cores dele. Fica nos recursos
    /// da janela da ilha, porque os recursos da aplicação congelam os pincéis (sem animação).
    /// </summary>
    public LinearGradientBrush IslandBrush { get; } = new(Colors.Black, Colors.Black, new Point(0, 0), new Point(0, 1));

    public bool IsDark { get; private set; } = true;

    /// <summary>Disparado na thread de UI após aplicar um tema.</summary>
    public event EventHandler? ThemeChanged;

    public void Initialize(Application application)
    {
        _application = application;
        Apply(settings.Current);
        settings.Changed += OnSettingsChanged;
        tint.Changed += OnTintChanged;
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

        ApplyIslandBackground(current, accent);
        resources["IslandShadowOpacity"] = 0.7 * current.ShadowIntensity;

        ThemeChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// O fundo vem do tema da ilha (sempre escuro) ou, se o usuário pediu, da capa da mídia tocando;
    /// a transparência só altera o alfa.
    /// </summary>
    private void ApplyIslandBackground(IslandSettings current, Color accent)
    {
        var brush = IslandBrush;
        var media = current.IslandThemeFollowsMedia ? TintColor() : null;
        var (top, bottom) = media is { } color ? IslandThemes.Tinted(color) : IslandThemes.Colors(current.IslandTheme, accent);
        var alpha = (byte)Math.Round(255 * current.IslandOpacity);
        top.A = alpha;
        bottom.A = alpha;

        brush.EndPoint = media is null && IslandThemes.IsHorizontal(current.IslandTheme) ? new Point(1, 0) : new Point(0, 1);
        Fade(brush.GradientStops[0], top, current.AnimationsEnabled);
        Fade(brush.GradientStops[1], bottom, current.AnimationsEnabled);

        // Cópia congelada para quem usa o recurso fora da janela da ilha.
        var snapshot = new LinearGradientBrush(top, bottom, brush.StartPoint, brush.EndPoint);
        snapshot.Freeze();
        _application?.Resources["IslandBackgroundBrush"] = snapshot;
    }

    private static void Fade(GradientStop stop, Color to, bool animate)
    {
        if (animate)
        {
            stop.BeginAnimation(GradientStop.ColorProperty, new ColorAnimation(to, IslandColorFade));
        }
        else
        {
            stop.BeginAnimation(GradientStop.ColorProperty, null);
            stop.Color = to;
        }
    }

    private Color? TintColor()
    {
        try
        {
            return tint.Color is { } hex ? Parse(hex) : null;
        }
        catch (FormatException)
        {
            return null;
        }
    }

    private void OnTintChanged(object? sender, EventArgs e) =>
        _application?.Dispatcher.BeginInvoke(() => ApplyIslandBackground(settings.Current, Parse(settings.Current.AccentColor)));

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
        tint.Changed -= OnTintChanged;
        SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
    }
}
