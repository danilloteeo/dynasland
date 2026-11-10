using Dynasland.Core.Models;

namespace Dynasland.Core.Interfaces;

/// <summary>
/// Histórico próprio da área de transferência (texto e imagem), compartilhado pela ilha e pela Central.
/// Respeita as configurações de privacidade: desligado, nada é capturado.
/// </summary>
public interface IClipboardHistory
{
    /// <summary>Itens incluídos, alterados ou removidos. Pode ser disparado em qualquer thread.</summary>
    event EventHandler? Changed;

    Task<IReadOnlyList<ClipboardRecord>> ListAsync(string? search = null, bool favoritesOnly = false, int limit = 200, CancellationToken cancellationToken = default);

    /// <summary>Coloca o item na área de transferência (sem gerar novo aviso na ilha).</summary>
    Task<bool> CopyAsync(long id, CancellationToken cancellationToken = default);

    Task SetFavoriteAsync(long id, bool favorite, CancellationToken cancellationToken = default);

    Task DeleteAsync(long id, CancellationToken cancellationToken = default);

    Task ClearAsync(bool keepFavorites, CancellationToken cancellationToken = default);
}

public enum HubPage
{
    Clipboard,
    Notifications,
    Calendar,
    Focus,
    Captures,
}

/// <summary>Abre a Central (janela com históricos e agenda) numa página.</summary>
public interface IHubService
{
    void Open(HubPage page);
}
