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

/// <summary>Instalador publicado junto com uma versão.</summary>
/// <param name="Size">Tamanho em bytes informado pela publicação.</param>
/// <param name="Sha256">Hash SHA-256 em hexadecimal, quando a publicação informa.</param>
public sealed record UpdateAsset(string Name, string Url, long Size, string? Sha256);

/// <param name="LatestVersion">Versão mais recente publicada, quando conhecida.</param>
/// <param name="ReleaseUrl">Página da versão para baixar.</param>
/// <param name="Installer">Instalador da versão, para atualizar sem sair do aplicativo.</param>
public sealed record UpdateCheckResult(UpdateStatus Status, string? LatestVersion, string? ReleaseUrl, string? Message = null, UpdateAsset? Installer = null);

public interface IUpdateChecker
{
    bool IsConfigured { get; }

    /// <summary>Endereço público onde as versões são publicadas.</summary>
    string? SourceDescription { get; }

    Task<UpdateCheckResult> CheckAsync(CancellationToken cancellationToken = default);
}

/// <summary>O arquivo baixado não confere com o que a publicação informa (tamanho ou hash).</summary>
public sealed class UpdateVerificationException(string message) : Exception(message);

/// <summary>Baixa o instalador de uma versão e o executa em modo silencioso.</summary>
public interface IUpdateInstaller
{
    /// <summary>
    /// <c>true</c> quando este aplicativo foi instalado pelo instalador (e portanto pode ser atualizado
    /// por ele). A versão portátil não pode: ela só abre a página de download.
    /// </summary>
    bool CanInstall { get; }

    /// <summary>Baixa e confere o instalador; devolve o caminho do arquivo.</summary>
    /// <param name="progress">Fração concluída, de 0 a 1.</param>
    /// <exception cref="UpdateVerificationException">Tamanho ou hash diferentes do publicado.</exception>
    Task<string> DownloadAsync(UpdateAsset asset, IProgress<double>? progress = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Inicia o instalador em modo silencioso. Ele fecha o aplicativo, copia os arquivos e o reabre;
    /// quem chama deve encerrar o aplicativo em seguida.
    /// </summary>
    void Launch(string installerPath, string version);

    /// <summary>
    /// Na partida: se a última execução terminou em uma atualização concluída, devolve a versão
    /// instalada (uma única vez) e apaga o instalador baixado.
    /// </summary>
    string? ConsumeCompletedUpdate(Version currentVersion);
}
