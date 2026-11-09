namespace Dynasland.Core.Models;

/// <summary>Notificação do Windows lida da Central de Ações.</summary>
/// <param name="Id">Identificador da notificação no Windows (usado para dispensá-la).</param>
/// <param name="AppId">AppUserModelId do aplicativo de origem.</param>
/// <param name="AppLogo">Bytes do ícone do aplicativo, quando disponível.</param>
public sealed record NotificationInfo(
    uint Id,
    string AppId,
    string AppName,
    string? Title,
    string Body,
    DateTimeOffset Time,
    byte[]? AppLogo);

public enum NotificationAccess
{
    Allowed,

    /// <summary>O usuário negou o acesso nas configurações de privacidade do Windows.</summary>
    Denied,

    Unavailable,
}
