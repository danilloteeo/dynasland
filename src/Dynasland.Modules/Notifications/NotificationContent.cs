using System.Globalization;
using System.Windows.Media;
using CommunityToolkit.Mvvm.Input;
using Dynasland.Core.Activities;
using Dynasland.Core.Layout;
using Dynasland.Core.Models;
using Dynasland.Modules.Common;

namespace Dynasland.Modules.Notifications;

/// <summary>Uma notificação na ilha. Imutável: cada notificação recebida gera um novo conteúdo.</summary>
/// <param name="accent">Cor dominante do ícone do aplicativo; <c>null</c> deixa o cartão neutro.</param>
/// <param name="duration">Tempo em que a notificação fica expandida na ilha.</param>
public sealed partial class NotificationContent(
    NotificationInfo info,
    ImageSource? logo,
    Color? accent,
    TimeSpan duration,
    bool showMessageText,
    Action<NotificationInfo> open,
    Action<NotificationInfo> dismiss,
    Action<NotificationInfo> muteApp) : IActivityContent
{
    private static readonly Brush NeutralBarBrush = Frozen(Color.FromArgb(0x59, 0xFF, 0xFF, 0xFF));

    public NotificationInfo Info { get; } = info;

    public ImageSource? Logo { get; } = logo;

    public bool HasLogo => Logo is not null;

    public string AppName => Info.AppName;

    /// <summary>Título da notificação ou, sem título, o nome do aplicativo.</summary>
    public string Headline => Info.Title ?? Info.AppName;

    public string Body { get; } = showMessageText ? info.Body : "Conteúdo oculto";

    /// <summary>Texto em uma linha corrida, para caber nas duas linhas do cartão expandido.</summary>
    public string ExpandedBody => string.Join(' ', Body.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

    public string TimeText => Info.Time.ToLocalTime().ToString("HH:mm", CultureInfo.GetCultureInfo("pt-BR"));

    public string AppLine => $"{AppName} · {TimeText}";

    public bool HasAccent { get; } = accent is not null;

    /// <summary>Cor do aplicativo para textos e para a barra de tempo; sem cor, um branco translúcido.</summary>
    public Brush AccentBrush { get; } = accent is { } color ? Frozen(color) : NeutralBarBrush;

    /// <summary>Centro do brilho atrás do ícone; transparente quando não há cor.</summary>
    public Color GlowColor { get; } = accent is { } glow ? Color.FromArgb(0x66, glow.R, glow.G, glow.B) : Colors.Transparent;

    /// <summary>Borda do brilho: a mesma cor sem opacidade, para o degradê não acinzentar.</summary>
    public Color GlowEdgeColor { get; } = accent is { } edge ? Color.FromArgb(0, edge.R, edge.G, edge.B) : Colors.Transparent;

    public TimeSpan Duration => Countdown.Duration;

    public CountdownSpan Countdown { get; } = new(duration);

    public bool CanMuteApp => !string.IsNullOrWhiteSpace(Info.AppId);

    public string FallbackGlyph => Glyphs.Notification;

    public IslandDimensions? GetPreferredDimensions(IslandState state) => state switch
    {
        IslandState.Expanded => new IslandDimensions(400, 92, 32),
        IslandState.Interactive => new IslandDimensions(410, 170, 36),
        _ => null,
    };

    [RelayCommand]
    private void Open() => open(Info);

    [RelayCommand]
    private void Dismiss() => dismiss(Info);

    [RelayCommand]
    private void MuteApp() => muteApp(Info);

    private static Brush Frozen(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }
}
