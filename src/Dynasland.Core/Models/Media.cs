namespace Dynasland.Core.Models;

public enum MediaPlaybackState
{
    Stopped,
    Playing,
    Paused,
    Changing,
}

/// <summary>Tipo informado pela sessão (muitos navegadores informam <see cref="Unknown"/>).</summary>
public enum MediaKind
{
    Unknown,
    Music,
    Video,
}

/// <summary>Estado de uma sessão de mídia do Windows (GSMTC).</summary>
/// <param name="SourceAppId">AppUserModelId do aplicativo que reproduz (ex.: <c>Spotify.exe</c>).</param>
/// <param name="AppName">Nome amigável do aplicativo.</param>
/// <param name="Thumbnail">Bytes da capa (PNG/JPEG), quando disponibilizada pela sessão.</param>
/// <param name="Position">Posição na última atualização da sessão; <c>null</c> se não informada.</param>
/// <param name="PositionUpdatedAt">Quando <paramref name="Position"/> foi medida (para extrapolar durante a reprodução).</param>
public sealed record MediaSnapshot(
    string SourceAppId,
    string AppName,
    string Title,
    string Artist,
    string AlbumTitle,
    byte[]? Thumbnail,
    MediaPlaybackState PlaybackState,
    TimeSpan? Position,
    TimeSpan? Duration,
    DateTimeOffset PositionUpdatedAt,
    bool CanPlay,
    bool CanPause,
    bool CanSkipNext,
    bool CanSkipPrevious)
{
    /// <summary>Identifica a sessão entre várias simultâneas (padrão: o aplicativo).</summary>
    public string SessionId { get; init; } = string.Empty;

    public MediaKind Kind { get; init; }

    public string Id => SessionId.Length > 0 ? SessionId : SourceAppId;

    public bool IsPlaying => PlaybackState == MediaPlaybackState.Playing;

    /// <summary>Identifica a faixa, para detectar troca de música.</summary>
    public string TrackKey => $"{SourceAppId}\u001f{Title}\u001f{Artist}\u001f{AlbumTitle}";

    /// <summary>Posição estimada em <paramref name="now"/>, avançando enquanto toca.</summary>
    public TimeSpan? EstimatePosition(DateTimeOffset now)
    {
        if (Position is not { } position)
        {
            return null;
        }

        var estimated = IsPlaying && now > PositionUpdatedAt ? position + (now - PositionUpdatedAt) : position;
        return Duration is { } duration && estimated > duration ? duration : estimated;
    }
}
