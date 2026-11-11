using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Text.Json;
using Dynasland.Core.Interfaces;
using Dynasland.Core.Services;
using Microsoft.Extensions.Logging;

namespace Dynasland.Infrastructure.Lyrics;

/// <summary>
/// Letras sincronizadas do LRCLIB (lrclib.net): serviço aberto, sem conta nem chave. A consulta envia
/// apenas título, artista, álbum e duração da música. O resultado (inclusive "não tem") fica em memória.
/// </summary>
public sealed class LrcLibLyricsService(HttpClient http, ILogger<LrcLibLyricsService> logger) : ILyricsService
{
    private const string BaseUrl = "https://lrclib.net/api/";
    private const int CacheLimit = 200;

    private readonly ConcurrentDictionary<string, IReadOnlyList<LyricLine>?> _cache = new(StringComparer.OrdinalIgnoreCase);

    public async Task<IReadOnlyList<LyricLine>?> GetAsync(string title, string artist, string? album, TimeSpan? duration, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(artist))
        {
            return null;
        }

        var key = $"{artist}\n{title}";
        if (_cache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        try
        {
            var lines = await GetExactAsync(title, artist, album, duration, cancellationToken)
                ?? await SearchAsync(title, artist, duration, cancellationToken);
            if (_cache.Count >= CacheLimit)
            {
                _cache.Clear();
            }

            _cache[key] = lines;
            return lines;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            // Sem internet ou serviço fora do ar: tenta de novo na próxima vez que a música tocar.
            logger.LogInformation("Letra indisponível para {Title}: {Reason}", title, ex.Message);
            return null;
        }
    }

    /// <summary>Consulta pela assinatura da faixa (o serviço exige a duração com tolerância de 2 s).</summary>
    private async Task<IReadOnlyList<LyricLine>?> GetExactAsync(string title, string artist, string? album, TimeSpan? duration, CancellationToken cancellationToken)
    {
        if (duration is not { } length || length <= TimeSpan.Zero)
        {
            return null;
        }

        var url = $"{BaseUrl}get?track_name={Uri.EscapeDataString(title)}&artist_name={Uri.EscapeDataString(artist)}"
            + $"&duration={Math.Round(length.TotalSeconds).ToString(CultureInfo.InvariantCulture)}"
            + (string.IsNullOrWhiteSpace(album) ? string.Empty : $"&album_name={Uri.EscapeDataString(album)}");

        using var response = await http.GetAsync(url, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        return ReadSynced(document.RootElement);
    }

    /// <summary>Busca por título e artista; fica com a versão de duração mais próxima que tenha letra sincronizada.</summary>
    private async Task<IReadOnlyList<LyricLine>?> SearchAsync(string title, string artist, TimeSpan? duration, CancellationToken cancellationToken)
    {
        var url = $"{BaseUrl}search?track_name={Uri.EscapeDataString(title)}&artist_name={Uri.EscapeDataString(artist)}";
        using var response = await http.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        if (document.RootElement.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        return PickClosest(document.RootElement, duration);
    }

    /// <summary>Entre os resultados com letra sincronizada, o de duração mais próxima (até 5 s de diferença, se a duração for conhecida).</summary>
    public static IReadOnlyList<LyricLine>? PickClosest(JsonElement results, TimeSpan? duration)
    {
        IReadOnlyList<LyricLine>? best = null;
        var bestDifference = double.MaxValue;
        foreach (var result in results.EnumerateArray())
        {
            if (ReadSynced(result) is not { } lines)
            {
                continue;
            }

            var difference = duration is { } length && result.TryGetProperty("duration", out var value) && value.ValueKind == JsonValueKind.Number
                ? Math.Abs(value.GetDouble() - length.TotalSeconds)
                : 0;
            if (difference < bestDifference && (duration is null || difference <= 5))
            {
                best = lines;
                bestDifference = difference;
            }
        }

        return best;
    }

    public static IReadOnlyList<LyricLine>? ReadSynced(JsonElement track)
    {
        if (track.ValueKind != JsonValueKind.Object
            || !track.TryGetProperty("syncedLyrics", out var synced) || synced.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        var lines = LrcParser.Parse(synced.GetString());
        return lines.Count > 0 ? lines : null;
    }
}
