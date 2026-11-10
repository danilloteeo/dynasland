// Contratos do ciclo de vida do aplicativo: iniciar com o Windows e verificação de atualizações.

namespace Dynasland.Core.Interfaces;

/// <summary>Registro para iniciar com o Windows (entrada "Run" do usuário, sem privilégios de administrador).</summary>
public interface IStartupRegistration
{
    bool IsEnabled { get; }

    void SetEnabled(bool enabled);
}

public enum UpdateStatus
{
    UpToDate,
    UpdateAvailable,

    /// <summary>O repositório existe mas ainda não tem versões publicadas (ou não está acessível).</summary>
    NoReleases,
    NotConfigured,
    Failed,
}

/// <param name="LatestVersion">Versão mais recente publicada, quando conhecida.</param>
/// <param name="ReleaseUrl">Página da versão para baixar.</param>
public sealed record UpdateCheckResult(UpdateStatus Status, string? LatestVersion, string? ReleaseUrl, string? Message = null);

public interface IUpdateChecker
{
    bool IsConfigured { get; }

    /// <summary>Endereço público onde as versões são publicadas.</summary>
    string? SourceDescription { get; }

    Task<UpdateCheckResult> CheckAsync(CancellationToken cancellationToken = default);
}
