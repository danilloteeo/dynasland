using System.Net;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using Dynasland.Core.Interfaces;
using Dynasland.Core.Services;
using Microsoft.Extensions.Logging;

namespace Dynasland.Infrastructure.Updates;

/// <summary>
/// Procura a versão mais recente nos Releases públicos do GitHub do repositório configurado em
/// <c>DynaslandUpdateRepository</c> (Directory.Build.props). A consulta envia apenas o pedido padrão
/// de HTTP — nenhum dado do usuário ou do computador.
/// </summary>
public sealed class GitHubUpdateChecker(ILogger<GitHubUpdateChecker> logger) : IUpdateChecker
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    private readonly string? _repository = typeof(GitHubUpdateChecker).Assembly
        .GetCustomAttributes<AssemblyMetadataAttribute>()
        .FirstOrDefault(a => a.Key == "UpdateRepository")?.Value is { Length: > 0 } repository ? repository : null;

    public bool IsConfigured => _repository is not null;

    public string? SourceDescription => _repository is null ? null : $"github.com/{_repository}/releases";

    public static Version CurrentVersion => Assembly.GetEntryAssembly()?.GetName().Version ?? new Version(0, 0, 0);

    public async Task<UpdateCheckResult> CheckAsync(CancellationToken cancellationToken = default)
    {
        if (_repository is null)
        {
            return new UpdateCheckResult(UpdateStatus.NotConfigured, null, null);
        }

        try
        {
            using var http = new HttpClient { Timeout = Timeout };
            http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Dynasland", CurrentVersion.ToString(3)));
            http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));

            using var response = await http.GetAsync($"https://api.github.com/repos/{_repository}/releases/latest", cancellationToken);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return new UpdateCheckResult(UpdateStatus.NoReleases, null, null);
            }

            response.EnsureSuccessStatusCode();
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var json = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            return ParseRelease(json.RootElement, CurrentVersion, _repository);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            logger.LogInformation("Verificação de atualizações falhou: {Message}", ex.Message);
            return new UpdateCheckResult(UpdateStatus.Failed, null, null, "Sem conexão com o GitHub no momento.");
        }
    }

    /// <summary>Interpreta a resposta de <c>releases/latest</c> da API do GitHub.</summary>
    public static UpdateCheckResult ParseRelease(JsonElement release, Version current, string repository)
    {
        var tag = release.TryGetProperty("tag_name", out var tagElement) ? tagElement.GetString() : null;
        var url = release.TryGetProperty("html_url", out var urlElement) ? urlElement.GetString() : null;

        if (ReleaseVersion.Parse(tag) is not { } latest)
        {
            return new UpdateCheckResult(UpdateStatus.NoReleases, null, null);
        }

        var status = ReleaseVersion.IsNewer(tag, current) ? UpdateStatus.UpdateAvailable : UpdateStatus.UpToDate;
        return new UpdateCheckResult(status, latest.ToString(3), url, Installer: FindInstaller(release, repository));
    }

    /// <summary>
    /// O instalador é o arquivo <c>DynaslandSetup-*.exe</c> da versão. Só vale se o endereço for o de
    /// download de versões do próprio repositório configurado.
    /// </summary>
    private static UpdateAsset? FindInstaller(JsonElement release, string repository)
    {
        if (!release.TryGetProperty("assets", out var assets) || assets.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var prefix = $"https://github.com/{repository}/releases/download/";
        foreach (var asset in assets.EnumerateArray())
        {
            var name = asset.TryGetProperty("name", out var nameElement) ? nameElement.GetString() : null;
            var url = asset.TryGetProperty("browser_download_url", out var urlElement) ? urlElement.GetString() : null;
            if (name is null || url is null
                || !name.StartsWith("DynaslandSetup-", StringComparison.OrdinalIgnoreCase)
                || !name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                || !url.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                || !asset.TryGetProperty("size", out var sizeElement) || !sizeElement.TryGetInt64(out var size) || size <= 0)
            {
                continue;
            }

            // "digest": "sha256:<hex>" (o GitHub informa para arquivos enviados a partir de 2025).
            var digest = asset.TryGetProperty("digest", out var digestElement) && digestElement.ValueKind == JsonValueKind.String
                ? digestElement.GetString()
                : null;
            var sha256 = digest is not null && digest.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase) && digest.Length == 7 + 64
                ? digest[7..].ToLowerInvariant()
                : null;
            return new UpdateAsset(name, url, size, sha256);
        }

        return null;
    }
}
