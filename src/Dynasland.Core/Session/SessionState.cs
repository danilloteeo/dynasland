namespace Dynasland.Core.Session;

public enum SessionChange
{
    /// <summary>Sessão bloqueada (Win+L, tela de bloqueio, troca de usuário).</summary>
    Locked,
    Unlocked,

    /// <summary>O computador vai entrar em suspensão ou hibernação.</summary>
    Suspending,
    Resumed,
}

/// <summary>Publicado no barramento quando a sessão é bloqueada/desbloqueada ou o computador suspende/retoma.</summary>
public sealed record SessionChangedEvent(SessionChange Change, bool IsInterrupted);

/// <summary>
/// Estado combinado de bloqueio e suspensão. A ilha fica oculta enquanto qualquer um estiver ativo:
/// ao retomar da suspensão com a tela de bloqueio aberta, continua oculta até o desbloqueio.
/// Não é thread-safe.
/// </summary>
public sealed class SessionState
{
    public bool IsLocked { get; private set; }

    public bool IsSuspended { get; private set; }

    public bool IsInterrupted => IsLocked || IsSuspended;

    /// <returns><c>true</c> se <see cref="IsInterrupted"/> mudou.</returns>
    public bool Apply(SessionChange change)
    {
        var before = IsInterrupted;
        switch (change)
        {
            case SessionChange.Locked:
                IsLocked = true;
                break;
            case SessionChange.Unlocked:
                IsLocked = false;
                break;
            case SessionChange.Suspending:
                IsSuspended = true;
                break;
            case SessionChange.Resumed:
                IsSuspended = false;
                break;
        }

        return before != IsInterrupted;
    }
}
