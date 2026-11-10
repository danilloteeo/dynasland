namespace Dynasland.Core.Interfaces;

// Integração opcional com a API Web do Spotify (fila de reprodução). O restante da mídia continua vindo
// dos controles de mídia do Windows; sem conexão, nada muda.

/// <param name="ImageUrl">Capa pequena (a menor disponível).</param>
public sealed record SpotifyTrack(string Name, string Artists, string? ImageUrl, TimeSpan Duration);

/// <summary>Faixa atual e as próximas na fila do Spotify.</summary>
public sealed record SpotifyQueue(SpotifyTrack? Current, IReadOnlyList<SpotifyTrack> Upcoming);

public enum SpotifyConnectionState
{
    /// <summary>Sem Client ID: o usuário ainda não configurou o próprio app do Spotify.</summary>
    NotConfigured,
    Disconnected,
    Connecting,
    Connected,
}

public interface ISpotifyService
{
    /// <summary>Endereço que o usuário cadastra no app do Spotify (Redirect URI).</summary>
    string RedirectUri { get; }

    SpotifyConnectionState State { get; }

    /// <summary>Nome da conta conectada, quando conhecido.</summary>
    string? AccountName { get; }

    /// <summary>Estado ou conta mudaram (qualquer thread).</summary>
    event EventHandler? Changed;

    /// <summary>Abre o login do Spotify no navegador e espera a autorização (até 3 minutos).</summary>
    /// <returns>Mensagem de erro para o usuário; <c>null</c> se conectou.</returns>
    Task<string?> ConnectAsync(CancellationToken cancellationToken = default);

    /// <summary>Esquece a conta (apaga o token guardado).</summary>
    Task DisconnectAsync();

    /// <summary>Fila atual; <c>null</c> sem conexão, sem player ativo ou se o Spotify não respondeu.</summary>
    Task<SpotifyQueue?> GetQueueAsync(CancellationToken cancellationToken = default);

    /// <summary>A autorização atual permite controlar o player (volume). Contas antigas precisam reconectar.</summary>
    bool CanControlPlayback { get; }

    /// <summary>Volume do player do Spotify (o do próprio app), 0 a 100; <c>null</c> sem player ativo.</summary>
    Task<int?> GetVolumeAsync(CancellationToken cancellationToken = default);

    /// <summary>Muda o volume do player do Spotify (exige Premium).</summary>
    /// <returns>Mensagem para o usuário em caso de falha; <c>null</c> se aplicou.</returns>
    Task<string?> SetVolumeAsync(int percent, CancellationToken cancellationToken = default);

    /// <summary>Baixa (com cache em memória) a imagem de uma faixa da fila.</summary>
    Task<byte[]?> GetImageAsync(string url, CancellationToken cancellationToken = default);
}
