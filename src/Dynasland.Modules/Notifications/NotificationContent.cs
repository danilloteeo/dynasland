using System.Globalization;
using System.Windows.Media;
using CommunityToolkit.Mvvm.Input;
using Dynasland.Core.Activities;
using Dynasland.Core.Layout;
using Dynasland.Core.Models;
using Dynasland.Modules.Common;

namespace Dynasland.Modules.Notifications;

/// <summary>Uma notificação na ilha. Imutável: cada notificação recebida gera um novo conteúdo.</summary>
public sealed partial class NotificationContent(
    NotificationInfo info,
    ImageSource? logo,
    bool showMessageText,
    Action<NotificationInfo> open,
    Action<NotificationInfo> dismiss,
    Action<NotificationInfo> muteApp) : IActivityContent
{
    public NotificationInfo Info { get; } = info;

    public ImageSource? Logo { get; } = logo;

    public bool HasLogo => Logo is not null;

    public string AppName => Info.AppName;

    /// <summary>Título da notificação ou, sem título, o nome do aplicativo.</summary>
    public string Headline => Info.Title ?? Info.AppName;

    public string Body => showMessageText ? Info.Body : "Conteúdo oculto";

    public string TimeText => Info.Time.ToLocalTime().ToString("HH:mm", CultureInfo.GetCultureInfo("pt-BR"));

    public bool CanMuteApp => !string.IsNullOrWhiteSpace(Info.AppId);

    public string FallbackGlyph => Glyphs.Notification;

    public IslandDimensions? GetPreferredDimensions(IslandState state) => state switch
    {
        IslandState.Expanded => new IslandDimensions(380, 76, 30),
        IslandState.Interactive => new IslandDimensions(410, 170, 36),
        _ => null,
    };

    [RelayCommand]
    private void Open() => open(Info);

    [RelayCommand]
    private void Dismiss() => dismiss(Info);

    [RelayCommand]
    private void MuteApp() => muteApp(Info);
}
