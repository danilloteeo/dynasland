using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dynasland.Core.Interfaces;
using Dynasland.Infrastructure.Database;
using Microsoft.Extensions.Logging;

namespace Dynasland.Infrastructure.Spotify;

/// <summary>
/// API Web do Spotify com o app (Client ID) criado pelo próprio usuário. Autorização OAuth com PKCE (sem
/// segredo) por um endereço de retorno local (127.0.0.1); o token fica criptografado com a proteção de
/// dados do Windows (DPAPI, só este usuário deste computador consegue ler). Escopos somente de leitura.
/// </summary>
public sealed class SpotifyService : ISpotifyService, IDisposable
{
    public const int CallbackPort = 43821;
    private const string Scopes = "user-read-playback-state user-read-currently-playing";
    private const string AuthorizeUrl = "https://accounts.spotify.com/authorize";
    private const string TokenUrl = "https://accounts.spotify.com/api/token";
    private const string ApiUrl = "https://api.spotify.com/v1";
    private static readonly TimeSpan LoginTimeout = TimeSpan.FromMinutes(3);
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;
    private readonly IIslandSettingsService _settings;
    private readonly IShellService _shell;
    private readonly ILogger<SpotifyService> _logger;
    private readonly string _tokenPath;
    private readonly SemaphoreSlim _tokenGate = new(1, 1);
    private readonly ConcurrentDictionary<string, byte[]> _images = new();
    private TokenSet? _token;
    private bool _connecting;

    public SpotifyService(HttpClient http, IIslandSettingsService settings, IShellService shell, DatabaseOptions database, ILogger<SpotifyService> logger)
    {
        _http = http;
        _settings = settings;
        _shell = shell;
        _logger = logger;
        _tokenPath = Path.Combine(Path.GetDirectoryName(database.DatabasePath)!, "spotify-token.bin");
        _token = LoadToken();
        settings.Changed += (_, _) => Changed?.Invoke(this, EventArgs.Empty);
    }

    public string RedirectUri => $"http://127.0.0.1:{CallbackPort}/callback";

    public SpotifyConnectionState State => ClientId is null
        ? SpotifyConnectionState.NotConfigured
        : _connecting ? SpotifyConnectionState.Connecting
        : _token is null ? SpotifyConnectionState.Disconnected
        : SpotifyConnectionState.Connected;

    public string? AccountName => _token?.AccountName;

    public event EventHandler? Changed;

    private string? ClientId => _settings.Current.SpotifyClientId;

    public async Task<string?> ConnectAsync(CancellationToken cancellationToken = default)
    {
        if (ClientId is not { } clientId)
        {
            return "Informe o Client ID do seu app do Spotify primeiro.";
        }

        if (_connecting)
        {
            return null;
        }

        _connecting = true;
        Changed?.Invoke(this, EventArgs.Empty);
        TcpListener? listener = null;
        try
        {
            var verifier = Base64Url(RandomNumberGenerator.GetBytes(48));
            var challenge = Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
            var state = Base64Url(RandomNumberGenerator.GetBytes(16));

            try
            {
                listener = new TcpListener(IPAddress.Loopback, CallbackPort);
                listener.Start();
            }
            catch (SocketException)
            {
                return $"A porta {CallbackPort} está em uso por outro programa.";
            }

            var url = $"{AuthorizeUrl}?client_id={Uri.EscapeDataString(clientId)}&response_type=code" +
                $"&redirect_uri={Uri.EscapeDataString(RedirectUri)}&code_challenge_method=S256&code_challenge={challenge}" +
                $"&state={state}&scope={Uri.EscapeDataString(Scopes)}";
            _shell.OpenUrl(url);

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(LoginTimeout);
            var query = await WaitForCallbackAsync(listener, timeout.Token);

            if (query.TryGetValue("error", out var error))
            {
                return error == "access_denied" ? "A autorização foi recusada no Spotify." : $"O Spotify recusou: {error}.";
            }

            if (query.GetValueOrDefault("state") != state || !query.TryGetValue("code", out var code))
            {
                return "Resposta inválida do Spotify. Tente de novo.";
            }

            var token = await RequestTokenAsync(new Dictionary<string, string>
            {
                ["grant_type"] = "authorization_code",
                ["code"] = code,
                ["redirect_uri"] = RedirectUri,
                ["client_id"] = clientId,
                ["code_verifier"] = verifier,
            }, previousRefreshToken: null, cancellationToken);
            if (token is null)
            {
                return "Não foi possível obter o acesso. Confira o Client ID e o Redirect URI cadastrados no app do Spotify.";
            }

            token = token with { AccountName = await ReadAccountNameAsync(token.AccessToken, cancellationToken) };
            _token = token;
            SaveToken(token);
            return null;
        }
        catch (OperationCanceledException)
        {
            return "Tempo esgotado esperando a autorização no navegador.";
        }
        catch (HttpRequestException)
        {
            return "Sem conexão com o Spotify.";
        }
        finally
        {
            listener?.Stop();
            _connecting = false;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    public Task DisconnectAsync()
    {
        _token = null;
        try
        {
            File.Delete(_tokenPath);
        }
        catch (IOException)
        {
            // Será sobrescrito no próximo login.
        }

        Changed?.Invoke(this, EventArgs.Empty);
        return Task.CompletedTask;
    }

    public async Task<SpotifyQueue?> GetQueueAsync(CancellationToken cancellationToken = default)
    {
        if (await ValidTokenAsync(cancellationToken) is not { } token)
        {
            return null;
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"{ApiUrl}/me/player/queue");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            using var response = await _http.SendAsync(request, cancellationToken);
            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                await DisconnectAsync();
                return null;
            }

            if (!response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.NoContent)
            {
                return null;
            }

            var queue = await response.Content.ReadFromJsonAsync<QueueResponse>(Json, cancellationToken);
            return queue is null ? null : new SpotifyQueue(Map(queue.CurrentlyPlaying), [.. (queue.Queue ?? []).Select(Map).OfType<SpotifyTrack>()]);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            _logger.LogInformation("Fila do Spotify indisponível: {Message}", ex.Message);
            return null;
        }
    }

    public async Task<byte[]?> GetImageAsync(string url, CancellationToken cancellationToken = default)
    {
        if (_images.TryGetValue(url, out var cached))
        {
            return cached;
        }

        try
        {
            var bytes = await _http.GetByteArrayAsync(url, cancellationToken);
            if (_images.Count > 100)
            {
                _images.Clear();
            }

            _images[url] = bytes;
            return bytes;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return null;
        }
    }

    public void Dispose() => _tokenGate.Dispose();

    internal static SpotifyTrack? Map(TrackItem? item)
    {
        if (item?.Name is not { } name)
        {
            return null;
        }

        var artists = item.Artists is { Count: > 0 } list
            ? string.Join(", ", list.Select(a => a.Name))
            : item.Show?.Name ?? string.Empty;
        var images = item.Album?.Images ?? item.Images ?? item.Show?.Images;
        var image = images?.Where(i => i.Url is not null).OrderBy(i => i.Width ?? int.MaxValue).FirstOrDefault()?.Url;
        return new SpotifyTrack(name, artists, image, TimeSpan.FromMilliseconds(item.DurationMs));
    }

    /// <summary>Atende uma única requisição HTTP no endereço local e devolve os parâmetros da URL.</summary>
    private static async Task<Dictionary<string, string>> WaitForCallbackAsync(TcpListener listener, CancellationToken cancellationToken)
    {
        while (true)
        {
            using var client = await listener.AcceptTcpClientAsync(cancellationToken);
            await using var stream = client.GetStream();
            using var reader = new StreamReader(stream, Encoding.ASCII, leaveOpen: true);
            var requestLine = await reader.ReadLineAsync(cancellationToken) ?? string.Empty;
            var parts = requestLine.Split(' ');
            if (parts.Length < 2 || !parts[1].StartsWith("/callback", StringComparison.Ordinal))
            {
                await RespondAsync(stream, "404 Not Found", "Não encontrado.", cancellationToken);
                continue;
            }

            var query = ParseQuery(parts[1]);
            var ok = query.ContainsKey("code");
            await RespondAsync(stream, "200 OK",
                ok ? "Dynasland conectado ao Spotify. Pode fechar esta aba." : "A conexão com o Spotify não foi concluída. Pode fechar esta aba.",
                cancellationToken);
            return query;
        }
    }

    internal static Dictionary<string, string> ParseQuery(string pathAndQuery)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        var index = pathAndQuery.IndexOf('?');
        if (index < 0)
        {
            return result;
        }

        foreach (var pair in pathAndQuery[(index + 1)..].Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = pair.IndexOf('=');
            var key = Uri.UnescapeDataString(separator < 0 ? pair : pair[..separator]);
            var value = separator < 0 ? string.Empty : Uri.UnescapeDataString(pair[(separator + 1)..].Replace('+', ' '));
            result[key] = value;
        }

        return result;
    }

    private static async Task RespondAsync(NetworkStream stream, string status, string message, CancellationToken cancellationToken)
    {
        var html = $"<!doctype html><meta charset=\"utf-8\"><title>Dynasland</title><body style=\"font-family:Segoe UI;background:#111;color:#eee;padding:40px\"><h2>{WebUtility.HtmlEncode(message)}</h2></body>";
        var body = Encoding.UTF8.GetBytes(html);
        var header = Encoding.ASCII.GetBytes($"HTTP/1.1 {status}\r\nContent-Type: text/html; charset=utf-8\r\nContent-Length: {body.Length}\r\nConnection: close\r\n\r\n");
        await stream.WriteAsync(header, cancellationToken);
        await stream.WriteAsync(body, cancellationToken);
    }

    /// <summary>Token válido, renovando-o (refresh token) quando estiver para vencer.</summary>
    private async Task<string?> ValidTokenAsync(CancellationToken cancellationToken)
    {
        if (_token is not { } token || ClientId is not { } clientId)
        {
            return null;
        }

        if (token.ExpiresAt > DateTimeOffset.UtcNow.AddMinutes(1))
        {
            return token.AccessToken;
        }

        await _tokenGate.WaitAsync(cancellationToken);
        try
        {
            if (_token is { } current && current.ExpiresAt > DateTimeOffset.UtcNow.AddMinutes(1))
            {
                return current.AccessToken;
            }

            var refreshed = await RequestTokenAsync(new Dictionary<string, string>
            {
                ["grant_type"] = "refresh_token",
                ["refresh_token"] = token.RefreshToken,
                ["client_id"] = clientId,
            }, token.RefreshToken, cancellationToken);

            if (refreshed is null)
            {
                await DisconnectAsync();
                return null;
            }

            _token = refreshed with { AccountName = token.AccountName };
            SaveToken(_token);
            return _token.AccessToken;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return null;
        }
        finally
        {
            _tokenGate.Release();
        }
    }

    private async Task<TokenSet?> RequestTokenAsync(Dictionary<string, string> form, string? previousRefreshToken, CancellationToken cancellationToken)
    {
        using var response = await _http.PostAsync(TokenUrl, new FormUrlEncodedContent(form), cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Spotify recusou o token: {Status} {Body}", response.StatusCode, await response.Content.ReadAsStringAsync(cancellationToken));
            return null;
        }

        var token = await response.Content.ReadFromJsonAsync<TokenResponse>(Json, cancellationToken);
        if (token?.AccessToken is not { } access)
        {
            return null;
        }

        // Na renovação, o Spotify pode omitir um novo refresh token: o anterior continua valendo.
        var refresh = token.RefreshToken ?? previousRefreshToken ?? string.Empty;
        return new TokenSet(access, refresh, DateTimeOffset.UtcNow.AddSeconds(token.ExpiresIn), null);
    }

    private async Task<string?> ReadAccountNameAsync(string accessToken, CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"{ApiUrl}/me");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            using var response = await _http.SendAsync(request, cancellationToken);
            return response.IsSuccessStatusCode
                ? (await response.Content.ReadFromJsonAsync<ProfileResponse>(Json, cancellationToken))?.DisplayName
                : null;
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException)
        {
            return null;
        }
    }

    private TokenSet? LoadToken()
    {
        try
        {
            if (!File.Exists(_tokenPath))
            {
                return null;
            }

            var json = ProtectedData.Unprotect(File.ReadAllBytes(_tokenPath), null, DataProtectionScope.CurrentUser);
            return JsonSerializer.Deserialize<TokenSet>(json, Json);
        }
        catch (Exception ex) when (ex is CryptographicException or JsonException or IOException)
        {
            _logger.LogInformation("Token do Spotify ilegível; será preciso conectar de novo.");
            return null;
        }
    }

    private void SaveToken(TokenSet token)
    {
        try
        {
            var protectedBytes = ProtectedData.Protect(JsonSerializer.SerializeToUtf8Bytes(token, Json), null, DataProtectionScope.CurrentUser);
            File.WriteAllBytes(_tokenPath, protectedBytes);
        }
        catch (Exception ex) when (ex is CryptographicException or IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "Não foi possível guardar o token do Spotify.");
        }
    }

    private static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    internal sealed record TokenSet(string AccessToken, string RefreshToken, DateTimeOffset ExpiresAt, string? AccountName);

    private sealed record TokenResponse(
        [property: JsonPropertyName("access_token")] string? AccessToken,
        [property: JsonPropertyName("refresh_token")] string? RefreshToken,
        [property: JsonPropertyName("expires_in")] int ExpiresIn);

    private sealed record ProfileResponse([property: JsonPropertyName("display_name")] string? DisplayName);

    internal sealed record QueueResponse(
        [property: JsonPropertyName("currently_playing")] TrackItem? CurrentlyPlaying,
        [property: JsonPropertyName("queue")] List<TrackItem>? Queue);

    internal sealed record TrackItem(
        [property: JsonPropertyName("name")] string? Name,
        [property: JsonPropertyName("duration_ms")] int DurationMs,
        [property: JsonPropertyName("artists")] List<NamedItem>? Artists,
        [property: JsonPropertyName("album")] AlbumItem? Album,
        [property: JsonPropertyName("images")] List<ImageItem>? Images,
        [property: JsonPropertyName("show")] ShowItem? Show);

    internal sealed record NamedItem([property: JsonPropertyName("name")] string? Name);

    internal sealed record AlbumItem([property: JsonPropertyName("images")] List<ImageItem>? Images);

    internal sealed record ShowItem([property: JsonPropertyName("name")] string? Name, [property: JsonPropertyName("images")] List<ImageItem>? Images);

    internal sealed record ImageItem([property: JsonPropertyName("url")] string? Url, [property: JsonPropertyName("width")] int? Width);
}
