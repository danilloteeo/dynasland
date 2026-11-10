using System.Net.Http;
using Dynasland.App.Services;
using Dynasland.App.ViewModels;
using Dynasland.Core.Activities;
using Dynasland.Core.Interfaces;
using Dynasland.Tests.Modules;
using Microsoft.Extensions.Logging.Abstractions;

namespace Dynasland.Tests.Core;

public sealed class UpdateFlowTests : IDisposable
{
    private static readonly UpdateAsset Asset = new("DynaslandSetup-1.1.0.exe", "https://github.com/dono/dynasland/releases/download/v1.1.0/DynaslandSetup-1.1.0.exe", 100, null);
    private static readonly UpdateCheckResult Available = new(UpdateStatus.UpdateAvailable, "1.1.0", "https://github.com/dono/dynasland/releases/tag/v1.1.0", Installer: Asset);

    private readonly ActivityRecorder _recorder = new();
    private readonly FakeInstaller _installer = new();
    private readonly FakeShell _shell = new();
    private readonly FakeChecker _checker = new();
    private readonly UpdateFlow _flow;
    private readonly UpdateNotifier _notifier;
    private int _exitRequests;

    public UpdateFlowTests()
    {
        _flow = new UpdateFlow(_installer, _shell, new InlineDispatcher(), NullLogger<UpdateFlow>.Instance);
        _flow.ExitRequested += (_, _) => _exitRequests++;
        _notifier = new UpdateNotifier(_checker, _installer, _flow, new FakeIslandSettings(), _recorder.Bus, _shell, NullLogger<UpdateNotifier>.Instance);
    }

    public void Dispose() => _recorder.Dispose();

    [Fact]
    public async Task Installed_copy_downloads_installs_and_asks_to_exit()
    {
        _flow.Offer(Available);
        Assert.Equal(UpdateStage.Available, _flow.Stage);
        Assert.Equal("Atualizar agora", _flow.ActionLabel);

        var install = _flow.InstallCommand.ExecuteAsync(null);
        Assert.Equal(UpdateStage.Downloading, _flow.Stage);
        Assert.False(_flow.InstallCommand.CanExecute(null));

        _installer.Progress!.Report(0.42);
        Assert.Equal(42, _flow.Percent);
        Assert.Equal("Dynasland 1.1.0 · 42%", _flow.Detail);

        _installer.Download.SetResult(@"C:\updates\DynaslandSetup-1.1.0.exe");
        await install;

        Assert.Equal(UpdateStage.Installing, _flow.Stage);
        Assert.Equal((@"C:\updates\DynaslandSetup-1.1.0.exe", "1.1.0"), _installer.Launched);
        Assert.Equal(1, _exitRequests);
        Assert.Empty(_shell.Urls);
    }

    [Fact]
    public async Task Island_shows_the_download_as_a_live_activity()
    {
        _flow.Offer(Available);

        var install = _flow.InstallCommand.ExecuteAsync(null);

        Assert.Contains(UpdateNotifier.ActivityId, _recorder.Removed);
        var progress = Assert.Single(_recorder.PostedWithId(UpdateNotifier.ProgressActivityId));
        Assert.Equal(ActivityKind.Live, progress.Kind);
        Assert.False(progress.CanDismiss);
        Assert.Same(_flow, Assert.IsType<UpdateAvailableContent>(progress.Content).Flow);

        _installer.Download.SetResult("x");
        await install;
    }

    [Fact]
    public async Task Failed_download_can_be_retried_and_returns_to_the_notice()
    {
        _flow.Offer(Available);
        _installer.Download.SetException(new HttpRequestException("sem rede"));

        await _flow.InstallCommand.ExecuteAsync(null);

        Assert.Equal(UpdateStage.Failed, _flow.Stage);
        Assert.Equal("Tentar de novo", _flow.ActionLabel);
        Assert.True(_flow.InstallCommand.CanExecute(null));
        Assert.Null(_installer.Launched);
        Assert.Equal(0, _exitRequests);
        Assert.Contains(UpdateNotifier.ProgressActivityId, _recorder.Removed);
        Assert.Single(_recorder.PostedWithId(UpdateNotifier.ActivityId));

        _installer.Download = new TaskCompletionSource<string>();
        _installer.Download.SetResult("x");
        await _flow.InstallCommand.ExecuteAsync(null);

        Assert.Equal(UpdateStage.Installing, _flow.Stage);
    }

    [Fact]
    public async Task Tampered_download_shows_the_reason_and_never_runs()
    {
        _flow.Offer(Available);
        _installer.Download.SetException(new UpdateVerificationException("O arquivo baixado não confere com o publicado."));

        await _flow.InstallCommand.ExecuteAsync(null);

        Assert.Equal("O arquivo baixado não confere com o publicado.", _flow.Detail);
        Assert.Null(_installer.Launched);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task Portable_copy_or_release_without_installer_opens_the_page(bool installed, bool hasAsset)
    {
        _installer.CanInstall = installed;
        _flow.Offer(hasAsset ? Available : Available with { Installer = null });
        Assert.Equal("Baixar", _flow.ActionLabel);

        await _flow.InstallCommand.ExecuteAsync(null);

        Assert.Equal(["https://github.com/dono/dynasland/releases/tag/v1.1.0"], _shell.Urls);
        Assert.Equal(UpdateStage.Available, _flow.Stage);
        Assert.Equal(0, _installer.Downloads);
    }

    [Fact]
    public async Task Offer_is_ignored_while_busy_and_cleared_when_up_to_date()
    {
        _flow.Offer(Available);
        var install = _flow.InstallCommand.ExecuteAsync(null);

        _flow.Offer(new UpdateCheckResult(UpdateStatus.UpToDate, "1.1.0", null));
        Assert.Equal(UpdateStage.Downloading, _flow.Stage);
        _installer.Download.SetResult("x");
        await install;

        var idle = new UpdateFlow(_installer, _shell, new InlineDispatcher(), NullLogger<UpdateFlow>.Instance);
        idle.Offer(Available);
        idle.Offer(new UpdateCheckResult(UpdateStatus.UpToDate, "1.1.0", null));
        Assert.Equal(UpdateStage.None, idle.Stage);
        Assert.False(idle.InstallCommand.CanExecute(null));
    }

    [Fact]
    public void Reopening_after_an_update_confirms_the_new_version_once()
    {
        _installer.Completed = "1.1.0";

        _notifier.AnnounceCompletedUpdate();
        _notifier.AnnounceCompletedUpdate();

        var notice = Assert.Single(_recorder.PostedWithId(UpdateNotifier.CompletedActivityId));
        var content = Assert.IsType<StatusContent>(notice.Content);
        Assert.Equal("Versão 1.1.0 instalada.", content.Message);

        content.Action!.Execute(null);
        Assert.Equal(["https://github.com/dono/dynasland/releases/tag/v1.1.0"], _shell.Urls);
    }

    private sealed class FakeInstaller : IUpdateInstaller
    {
        public bool CanInstall { get; set; } = true;

        public TaskCompletionSource<string> Download { get; set; } = new();

        public IProgress<double>? Progress { get; private set; }

        public int Downloads { get; private set; }

        public (string Path, string Version)? Launched { get; private set; }

        public string? Completed { get; set; }

        public Task<string> DownloadAsync(UpdateAsset asset, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
        {
            Downloads++;
            Progress = progress;
            return Download.Task;
        }

        public void Launch(string installerPath, string version) => Launched = (installerPath, version);

        public string? ConsumeCompletedUpdate(Version currentVersion)
        {
            var completed = Completed;
            Completed = null;
            return completed;
        }
    }

    private sealed class FakeShell : IShellService
    {
        public List<string> Urls { get; } = [];

        public bool Open(string path) => true;

        public bool RevealInFolder(string path) => true;

        public bool OpenUrl(string url)
        {
            Urls.Add(url);
            return true;
        }
    }

    private sealed class FakeChecker : IUpdateChecker
    {
        public bool IsConfigured => true;

        public string? SourceDescription => "github.com/dono/dynasland/releases";

        public Task<UpdateCheckResult> CheckAsync(CancellationToken cancellationToken = default) => Task.FromResult(Available);
    }
}
