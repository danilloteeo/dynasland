using Dynasland.Core.Models;

// Contratos de utilidades: métricas do sistema, downloads, área de transferência, shell e som de alerta.

namespace Dynasland.Core.Interfaces;

public interface ISystemMetricsService
{
    /// <summary>Leitura completa (CPU, GPU, temperaturas, memória, discos). Chamar fora da thread de UI.</summary>
    SystemMetrics Sample();

    /// <summary>Leitura barata para verificações em segundo plano.</summary>
    MemoryUsage GetMemory();

    IReadOnlyList<DiskUsage> GetDisks();
}

/// <summary>
/// Fonte de downloads. Navegadores não expõem seus downloads de forma padronizada, então cada
/// integração (pasta Downloads, extensão de navegador, gerenciador de downloads...) é um provedor.
/// </summary>
public interface IDownloadProvider
{
    string Name { get; }

    event EventHandler<DownloadInfo>? Changed;

    /// <returns><c>false</c> se o provedor não puder funcionar neste computador.</returns>
    Task<bool> StartAsync(CancellationToken cancellationToken = default);

    Task StopAsync();
}

public interface IClipboardService
{
    /// <summary>
    /// O conteúdo mudou. Não é disparado para conteúdo marcado como privado
    /// (ex.: senhas copiadas por gerenciadores de senha).
    /// </summary>
    event EventHandler? ContentChanged;

    /// <summary>Histórico da área de transferência ativado nas configurações do Windows (Win+V).</summary>
    bool IsHistoryEnabled { get; }

    Task StartAsync(CancellationToken cancellationToken = default);

    Task StopAsync();

    Task<ClipboardEntry?> GetCurrentAsync();

    /// <summary>Itens mais recentes do histórico do Windows; vazio se o histórico estiver desativado.</summary>
    Task<IReadOnlyList<ClipboardEntry>> GetHistoryAsync(int maxItems);

    /// <summary>Coloca novamente na área de transferência um item do histórico.</summary>
    Task<bool> CopyAgainAsync(string entryId);

    Task<bool> SetTextAsync(string text);

    /// <param name="image">Imagem codificada (PNG, BMP...).</param>
    Task<bool> SetImageAsync(byte[] image);
}

/// <summary>Ações de shell: abrir arquivos e mostrá-los no Explorador.</summary>
public interface IShellService
{
    bool Open(string path);

    bool RevealInFolder(string path);

    /// <summary>Abre um endereço https no navegador padrão.</summary>
    bool OpenUrl(string url);
}

/// <summary>Som de alerta (ex.: fim do temporizador).</summary>
public interface IAlertSound
{
    void Play();
}
