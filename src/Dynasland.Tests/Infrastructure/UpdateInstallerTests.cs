using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using Dynasland.Core.Interfaces;
using Dynasland.Infrastructure.Updates;
using Microsoft.Extensions.Logging.Abstractions;

namespace Dynasland.Tests.Infrastructure;

public sealed class UpdateInstallerTests : IDisposable
{
    private const string Repository = "dono/dynasland";
    private const string AssetUrl = "https://github.com/dono/dynasland/releases/download/v1.1.0/DynaslandSetup-1.1.0.exe";

    private readonly string _root = Path.Combine(Path.GetTempPath(), "dynasland-update-" + Guid.NewGuid().ToString("N"));
    private readonly byte[] _payload = [.. Enumerable.Range(0, 200_000).Select(i => (byte)(i % 251))];

    private string Folder => Path.Combine(_root, "updates");

    private string AppDirectory => Path.Combine(_root, "app");

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public void Release_exposes_the_installer_with_size_and_hash()
    {
        var hash = new string('a', 64);
        var result = Parse($$"""
            {
              "tag_name": "v1.1.0",
              "html_url": "https://github.com/dono/dynasland/releases/tag/v1.1.0",
              "assets": [
                { "name": "Dynasland-1.1.0-portable.zip", "size": 10, "browser_download_url": "https://github.com/dono/dynasland/releases/download/v1.1.0/Dynasland-1.1.0-portable.zip" },
                { "name": "DynaslandSetup-1.1.0.exe", "size": 123, "digest": "sha256:{{hash}}", "browser_download_url": "{{AssetUrl}}" }
              ]
            }
            """);

        Assert.Equal(UpdateStatus.UpdateAvailable, result.Status);
        Assert.Equal("1.1.0", result.LatestVersion);
        Assert.Equal(new UpdateAsset("DynaslandSetup-1.1.0.exe", AssetUrl, 123, hash), result.Installer);
    }

    [Theory]
    [InlineData("https://github.com/outro/repo/releases/download/v1.1.0/DynaslandSetup-1.1.0.exe")]
    [InlineData("https://exemplo.com/DynaslandSetup-1.1.0.exe")]
    [InlineData("http://github.com/dono/dynasland/releases/download/v1.1.0/DynaslandSetup-1.1.0.exe")]
    public void Installer_from_another_address_is_ignored(string url)
    {
        var result = Parse($$"""
            { "tag_name": "v1.1.0", "html_url": "x", "assets": [ { "name": "DynaslandSetup-1.1.0.exe", "size": 123, "browser_download_url": "{{url}}" } ] }
            """);

        Assert.Equal(UpdateStatus.UpdateAvailable, result.Status);
        Assert.Null(result.Installer);
    }

    [Fact]
    public void Release_without_assets_or_digest_still_parses()
    {
        Assert.Null(Parse("""{ "tag_name": "v1.1.0", "html_url": "x" }""").Installer);

        var withoutDigest = Parse($$"""
            { "tag_name": "v1.0.0", "html_url": "x", "assets": [ { "name": "DynaslandSetup-1.0.0.exe", "size": 5, "digest": null, "browser_download_url": "https://github.com/dono/dynasland/releases/download/v1.0.0/DynaslandSetup-1.0.0.exe" } ] }
            """);
        Assert.Equal(UpdateStatus.UpToDate, withoutDigest.Status);
        Assert.Null(withoutDigest.Installer!.Sha256);
    }

    [Fact]
    public async Task Download_reports_progress_and_keeps_the_verified_file()
    {
        var installer = Create(_payload);
        var reported = new List<double>();

        var path = await installer.DownloadAsync(Asset(), new Collector(reported));

        Assert.Equal(Path.Combine(Folder, "DynaslandSetup-1.1.0.exe"), path);
        Assert.Equal(_payload, File.ReadAllBytes(path));
        Assert.Equal(1.0, reported[^1]);
        Assert.True(reported.SequenceEqual(reported.Order()));
        Assert.Empty(Directory.GetFiles(Folder, "*.partial"));
    }

    [Fact]
    public async Task Download_with_wrong_hash_or_size_is_rejected_and_removed()
    {
        var installer = Create(_payload);

        await Assert.ThrowsAsync<UpdateVerificationException>(() => installer.DownloadAsync(Asset(sha256: new string('0', 64))));
        await Assert.ThrowsAsync<UpdateVerificationException>(() => installer.DownloadAsync(Asset(size: _payload.Length + 1, sha256: null)));
        await Assert.ThrowsAsync<UpdateVerificationException>(() => installer.DownloadAsync(Asset(size: _payload.Length - 1, sha256: null)));

        Assert.Empty(Directory.GetFiles(Folder));
    }

    [Fact]
    public async Task Download_from_outside_github_is_refused_without_any_request()
    {
        var handler = new PayloadHandler(_payload);
        var installer = Create(handler);

        await Assert.ThrowsAsync<UpdateVerificationException>(() =>
            installer.DownloadAsync(new UpdateAsset("DynaslandSetup-1.1.0.exe", "https://exemplo.com/DynaslandSetup-1.1.0.exe", _payload.Length, null)));
        await Assert.ThrowsAsync<UpdateVerificationException>(() =>
            installer.DownloadAsync(new UpdateAsset("DynaslandSetup-1.1.0.exe", AssetUrl.Replace("https", "http"), _payload.Length, null)));

        Assert.Equal(0, handler.Requests);
    }

    [Fact]
    public async Task Asset_name_cannot_escape_the_updates_folder()
    {
        var installer = Create(_payload);

        var path = await installer.DownloadAsync(Asset() with { Name = @"..\..\DynaslandSetup-1.1.0.exe" });

        Assert.Equal(Path.Combine(Folder, "DynaslandSetup-1.1.0.exe"), path);
    }

    [Fact]
    public void Only_an_installed_copy_can_update_itself()
    {
        var installer = Create(_payload);
        Assert.False(installer.CanInstall);

        Directory.CreateDirectory(AppDirectory);
        File.WriteAllText(Path.Combine(AppDirectory, UpdateInstaller.UninstallerName), string.Empty);

        Assert.True(installer.CanInstall);
    }

    [Fact]
    public void Completed_update_is_announced_once_and_cleans_the_download()
    {
        var installer = Create(_payload);
        Directory.CreateDirectory(Folder);
        File.WriteAllText(Path.Combine(Folder, "pending.txt"), "1.1.0");
        File.WriteAllText(Path.Combine(Folder, "DynaslandSetup-1.1.0.exe"), "x");

        Assert.Equal("1.1.0", installer.ConsumeCompletedUpdate(new Version(1, 1, 0, 0)));
        Assert.Null(installer.ConsumeCompletedUpdate(new Version(1, 1, 0, 0)));
        Assert.Empty(Directory.GetFiles(Folder));
    }

    [Fact]
    public void Update_that_did_not_finish_is_not_announced()
    {
        var installer = Create(_payload);
        Assert.Null(installer.ConsumeCompletedUpdate(new Version(1, 0, 6)));

        Directory.CreateDirectory(Folder);
        File.WriteAllText(Path.Combine(Folder, "pending.txt"), "1.1.0");

        Assert.Null(installer.ConsumeCompletedUpdate(new Version(1, 0, 6, 0)));
        Assert.False(File.Exists(Path.Combine(Folder, "pending.txt")));
    }

    private static UpdateCheckResult Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        return GitHubUpdateChecker.ParseRelease(document.RootElement, new Version(1, 0, 6, 0), Repository);
    }

    private UpdateAsset Asset(long? size = null, string? sha256 = "") =>
        new("DynaslandSetup-1.1.0.exe", AssetUrl, size ?? _payload.Length, sha256 == string.Empty ? Convert.ToHexStringLower(SHA256.HashData(_payload)) : sha256);

    private UpdateInstaller Create(byte[] payload) => Create(new PayloadHandler(payload));

    private UpdateInstaller Create(HttpMessageHandler handler) =>
        new(new HttpClient(handler), new UpdateInstallerOptions(Folder, AppDirectory), NullLogger<UpdateInstaller>.Instance);

    private sealed class PayloadHandler(byte[] payload) : HttpMessageHandler
    {
        public int Requests { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(payload) });
        }
    }

    private sealed class Collector(List<double> values) : IProgress<double>
    {
        public void Report(double value) => values.Add(value);
    }
}
