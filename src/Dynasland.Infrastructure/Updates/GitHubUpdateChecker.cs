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
            var tag = json.RootElement.TryGetProperty("tag_name", out var tagElement) ? tagElement.GetString() : null;
            var url = json.RootElement.TryGetProperty("html_url", out var urlElement) ? urlElement.GetString() : null;

            if (ReleaseVersion.Parse(tag) is not { } latest)
            {
                return new UpdateCheckResult(UpdateStatus.NoReleases, null, null);
            }

            var status = ReleaseVersion.IsNewer(tag, CurrentVersion) ? UpdateStatus.UpdateAvailable : UpdateStatus.UpToDate;
            return new UpdateCheckResult(status, latest.ToString(3), url);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            logger.LogInformation("Verificação de atualizações falhou: {Message}", ex.Message);
            return new UpdateCheckResult(UpdateStatus.Failed, null, null, "Sem conexão com o GitHub no momento.");
        }
    }
}
