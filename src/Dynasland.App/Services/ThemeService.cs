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

    internal static readonly IReadOnlyDictionary<string, (string Dark, string Light)> Palette = new Dictionary<string, (string Dark, string Light)>
    {
        ["SettingsBackgroundBrush"] = ("#090909", "#FFFFFF"),
        ["SettingsSidebarBrush"] = ("#0D0D0D", "#F6F6F6"),
        ["SettingsCardBrush"] = ("#101010", "#F6F6F6"),
        ["SettingsCardHoverBrush"] = ("#161616", "#EFEFEF"),
        ["SettingsBorderBrush"] = ("#1C1C1C", "#E4E4E4"),
        ["SettingsTextBrush"] = ("#FFFFFF", "#111111"),
        ["SettingsSecondaryTextBrush"] = ("#B8B8B8", "#5C5C5C"),
        ["SettingsTertiaryTextBrush"] = ("#757575", "#8A8A8A"),
        ["SettingsNavHoverBrush"] = ("#161616", "#EDEDED"),
        ["SettingsButtonBrush"] = ("#161616", "#FFFFFF"),
        ["SettingsButtonHoverBrush"] = ("#1E1E1E", "#F0F0F0"),
        ["SettingsButtonBorderBrush"] = ("#262626", "#DADADA"),
        ["SettingsChipBrush"] = ("#161616", "#EFEFEF"),
        ["SettingsTrackBrush"] = ("#2A2A2A", "#D0D0D0"),
        ["SettingsScrollThumbBrush"] = ("#333333", "#C4C4C4"),
    };

    /// <summary>
    /// Cor de destaque efetiva: no tema claro um destaque quase branco (o padrão) viraria texto invisível
    /// sobre fundo branco, então usa o quase preto do Volum.
    /// </summary>
    public static Color AccentFor(Color accent, bool dark) =>
        !dark && (0.2126 * accent.R + 0.7152 * accent.G + 0.0722 * accent.B) / 255 > 0.9
            ? Color.FromRgb(0x11, 0x11, 0x11)
            : accent;

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

        var accent = AccentFor(Parse(current.AccentColor), IsDark);
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
        var animate = current.AnimationsEnabled && SystemParameters.ClientAreaAnimation;
        Fade(brush.GradientStops[0], top, animate);
        Fade(brush.GradientStops[1], bottom, animate);

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
