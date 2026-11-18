using System.Windows;
using System.Windows.Media;
using DynaIsland.Core.Interfaces;
using DynaIsland.Core.Models;
using Microsoft.Win32;

namespace DynaIsland.App.Services;

/// <summary>
/// Visual Vidro, sempre escuro: aplica a paleta das janelas, a cor de destaque e o vidro da ilha substituindo
/// os pincéis nos recursos da aplicação; tudo que usa DynamicResource é atualizado na hora.
/// </summary>
public sealed class ThemeService(IIslandSettingsService settings) : IDisposable
{
    /// <summary>Cor de base do vidro da ilha; o alfa vem da opacidade escolhida.</summary>
    public static readonly Color GlassBase = Color.FromRgb(0x10, 0x12, 0x1A);

    /// <summary>Texto sobre a cor de destaque (todas as cores prontas são claras).</summary>
    public static readonly Color OnAccent = Color.FromRgb(0x0D, 0x12, 0x20);

    /// <summary>Paleta das janelas WPF (Novidades e restos da Central): o fundo sólido usado quando não há acrílico.</summary>
    internal static readonly IReadOnlyDictionary<string, string> Palette = new Dictionary<string, string>
    {
        ["SettingsBackgroundBrush"] = "#14171F",
        ["SettingsSidebarBrush"] = "#0F1218",
        ["SettingsCardBrush"] = "#1A1E28",
        ["SettingsCardHoverBrush"] = "#212634",
        ["SettingsBorderBrush"] = "#262B3B",
        ["SettingsTextBrush"] = "#FFFFFF",
        ["SettingsSecondaryTextBrush"] = "#B4B8C4",
        ["SettingsTertiaryTextBrush"] = "#7D8291",
        ["SettingsNavHoverBrush"] = "#1C202B",
        ["SettingsButtonBrush"] = "#1F2330",
        ["SettingsButtonHoverBrush"] = "#272C3B",
        ["SettingsButtonBorderBrush"] = "#30364A",
        ["SettingsChipBrush"] = "#1F2330",
        ["SettingsTrackBrush"] = "#30364A",
        ["SettingsScrollThumbBrush"] = "#3A4055",
    };

    private Application? _application;

    /// <summary>
    /// Pincel mutável do vidro da ilha. Fica nos recursos da janela da ilha (os recursos da aplicação levam
    /// uma cópia congelada), e a opacidade muda na hora, sem recriar a janela.
    /// </summary>
    public SolidColorBrush IslandBrush { get; } = new(Glass(IslandSettings.Default.IslandOpacity));

    /// <summary>As janelas são sempre escuras (barra de título inclusive).</summary>
    public bool IsDark => true;

    /// <summary>Disparado na thread de UI após aplicar o tema.</summary>
    public event EventHandler? ThemeChanged;

    public void Initialize(Application application)
    {
        _application = application;
        Apply(settings.Current);
        settings.Changed += OnSettingsChanged;
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
    }

    /// <summary>A cor do vidro com a opacidade escolhida (mínimo de 50%, para a ilha continuar recebendo cliques).</summary>
    public static Color Glass(double opacity)
    {
        var color = GlassBase;
        color.A = (byte)Math.Round(255 * Math.Clamp(opacity, 0.5, 1), MidpointRounding.AwayFromZero);
        return color;
    }

    /// <summary>O destaque em uso: a cor do Windows, se pedida e disponível, ou a escolhida.</summary>
    public static string EffectiveAccent(IslandSettings current) =>
        current.AccentFollowsWindows && WindowsAccent.Read() is { } windows ? windows : current.AccentColor;

    private void Apply(IslandSettings current)
    {
        if (_application is null)
        {
            return;
        }

        var resources = _application.Resources;
        foreach (var (key, hex) in Palette)
        {
            resources[key] = Brush(Parse(hex));
        }

        var accent = Parse(EffectiveAccent(current));
        resources["SettingsAccentBrush"] = Brush(accent);
        resources["SettingsAccentTextBrush"] = Brush(OnAccent);
        resources["IslandAccentBrush"] = Brush(accent);
        resources["IslandOnAccentTextBrush"] = Brush(OnAccent);

        var glass = Glass(current.IslandOpacity);
        IslandBrush.Color = glass;
        resources["IslandBackgroundBrush"] = Brush(glass);
        resources["IslandShadowOpacity"] = 0.32;

        ThemeChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnSettingsChanged(object? sender, IslandSettings current) =>
        _application?.Dispatcher.BeginInvoke(() => Apply(current));

    /// <summary>Trocar a cor de destaque no Windows chega aqui; só importa quando o DynaIsland a acompanha.</summary>
    private void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        if (settings.Current.AccentFollowsWindows)
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
