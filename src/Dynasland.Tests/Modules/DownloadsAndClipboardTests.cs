using Dynasland.Core.Activities;
using Dynasland.Core.Interfaces;
using Dynasland.Core.Models;
using Dynasland.Core.Modules;
using Dynasland.Modules.Clipboard;
using Dynasland.Modules.Downloads;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace Dynasland.Tests.Modules;

public class DownloadsAndClipboardTests : IDisposable
{
    private readonly ActivityRecorder _recorder = new();

    public void Dispose() => _recorder.Dispose();

    private static DownloadInfo Download(string id, DownloadState state, long bytes = 1024, double? speed = null, long? total = null) =>
        new(id, "Fake", $"{id}.zip", $@"C:\Downloads\{id}.zip", bytes, total, speed, state, DateTimeOffset.UnixEpoch);

    [Fact]
    public async Task Download_lifecycle_shows_live_activity_then_completion()
    {
        var provider = new FakeDownloadProvider();
        var shell = new FakeShell();
        var module = new DownloadsModule([provider], shell, _recorder.Bus, new InlineDispatcher(), TestOptions.Create());
        await module.StartAsync(CancellationToken.None);

        provider.Raise(Download("a", DownloadState.InProgress, 1_000_000, speed: 500_000));
        provider.Raise(Download("a", DownloadState.InProgress, 2_000_000, speed: 500_000));
        provider.Raise(Download("b", DownloadState.InProgress));

        var live = Assert.Single(_recorder.Posted);
        Assert.Equal(DownloadsModule.ActiveActivityId, live.Id);
        Assert.True(live.ExpandOnStart);
        var content = Assert.IsType<DownloadsContent>(live.Content);
        Assert.Equal(2, content.Items.Count);
        Assert.Equal("2 downloads em andamento", content.Headline);

        provider.Raise(Download("a", DownloadState.Completed, 3_000_000));
        Assert.DoesNotContain(DownloadsModule.ActiveActivityId, _recorder.Removed);
        var completed = Assert.IsType<DownloadCompletedContent>(_recorder.Posted.Last().Content);
        Assert.Equal("a.zip", completed.FileName);

        completed.RevealCommand.Execute(null);
        Assert.Equal([@"C:\Downloads\a.zip"], shell.Revealed);

        provider.Raise(Download("b", DownloadState.Cancelled));
        Assert.Contains(DownloadsModule.ActiveActivityId, _recorder.Removed);
        Assert.Empty(content.Items);
    }

    [Fact]
    public void Progress_text_shows_only_what_the_provider_knows()
    {
        Assert.Equal("1,5 MB", DownloadItemViewModel.DescribeProgress(Download("x", DownloadState.InProgress, 1_572_864)));
        Assert.Equal("1,5 MB · 512 KB/s",
            DownloadItemViewModel.DescribeProgress(Download("x", DownloadState.InProgress, 1_572_864, speed: 524_288)));
        Assert.Equal("1 MB de 2 MB · 1 MB/s · menos de 1 min restantes",
            DownloadItemViewModel.DescribeProgress(Download("x", DownloadState.InProgress, 1_048_576, speed: 1_048_576, total: 2_097_152)));
    }

    [Fact]
    public async Task Downloads_module_is_unavailable_without_providers()
    {
        var module = new DownloadsModule([new FakeDownloadProvider { Available = false }], new FakeShell(), _recorder.Bus, new InlineDispatcher(), TestOptions.Create());

        await Assert.ThrowsAsync<ModuleUnavailableException>(() => module.StartAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Copy_shows_preview_and_own_copy_again_is_not_announced()
    {
        var time = new FakeTimeProvider();
        var clipboard = new FakeClipboard();
        var shortcuts = new ShortcutRegistry();
        var module = new ClipboardModule(clipboard, _recorder.Bus, new InlineDispatcher(), time, shortcuts, TestOptions.Create(), NullLogger<ClipboardModule>.Instance);
        await module.StartAsync(CancellationToken.None);

        clipboard.Current = new ClipboardEntry("", ClipboardEntryKind.Text, "linha 1\n   linha 2", null, DateTimeOffset.UnixEpoch);
        clipboard.RaiseChanged();
        await WaitUntilAsync(() => _recorder.Posted.Count == 1);

        var content = Assert.IsType<ClipboardContent>(_recorder.Posted[0].Content);
        Assert.Equal("linha 1 linha 2", content.LatestPreview);

        // Painel: histórico do Windows, clicar copia de novo sem gerar novo aviso.
        content.OnPresentationChanged(IslandState.Interactive);
        await WaitUntilAsync(() => content.History.Count == 2);
        await content.History[1].CopyCommand.ExecuteAsync(null);
        Assert.Equal(["h2"], clipboard.CopiedAgain);
        Assert.True(content.History[1].JustCopied);

        clipboard.RaiseChanged();
        await Task.Delay(50);
        Assert.Single(_recorder.Posted);

        time.Advance(TimeSpan.FromSeconds(2));
        clipboard.RaiseChanged();
        await WaitUntilAsync(() => _recorder.Posted.Count == 2);
    }

    [Fact]
    public async Task Disabled_windows_history_shows_guidance()
    {
        var clipboard = new FakeClipboard { HistoryEnabled = false };
        var module = new ClipboardModule(clipboard, _recorder.Bus, new InlineDispatcher(), new FakeTimeProvider(), new ShortcutRegistry(), TestOptions.Create(), NullLogger<ClipboardModule>.Instance);
        await module.StartAsync(CancellationToken.None);
        module.OpenPanel();
        var content = Assert.IsType<ClipboardContent>(_recorder.Posted.Single().Content);

        content.OnPresentationChanged(IslandState.Interactive);
        await WaitUntilAsync(() => content.HistoryMessage.Length > 0);

        Assert.Contains("Win+V", content.HistoryMessage);
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        for (var i = 0; i < 200 && !condition(); i++)
        {
            await Task.Delay(10);
        }

        Assert.True(condition());
    }

    private sealed class FakeDownloadProvider : IDownloadProvider
    {
        public bool Available { get; init; } = true;

        public string Name => "Fake";

        public event EventHandler<DownloadInfo>? Changed;

        public void Raise(DownloadInfo info) => Changed?.Invoke(this, info);

        public Task<bool> StartAsync(CancellationToken cancellationToken = default) => Task.FromResult(Available);

        public Task StopAsync() => Task.CompletedTask;
    }

    private sealed class FakeShell : IShellService
    {
        public List<string> Opened { get; } = [];

        public List<string> Revealed { get; } = [];

        public bool Open(string path)
        {
            Opened.Add(path);
            return true;
        }

        public bool OpenUrl(string url) => true;

        public bool RevealInFolder(string path)
        {
            Revealed.Add(path);
            return true;
        }
    }

    private sealed class FakeClipboard : IClipboardService
    {
        public ClipboardEntry? Current { get; set; }

        public bool HistoryEnabled { get; init; } = true;

        public List<string> CopiedAgain { get; } = [];

        public event EventHandler? ContentChanged;

        public bool IsHistoryEnabled => HistoryEnabled;

        public void RaiseChanged() => ContentChanged?.Invoke(this, EventArgs.Empty);

        public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task StopAsync() => Task.CompletedTask;

        public Task<ClipboardEntry?> GetCurrentAsync() => Task.FromResult(Current);

        public Task<IReadOnlyList<ClipboardEntry>> GetHistoryAsync(int maxItems) => Task.FromResult<IReadOnlyList<ClipboardEntry>>(
            HistoryEnabled
                ? [new("h1", ClipboardEntryKind.Text, "primeiro", null, DateTimeOffset.UnixEpoch), new("h2", ClipboardEntryKind.Text, "segundo", null, DateTimeOffset.UnixEpoch)]
                : []);

        public Task<bool> CopyAgainAsync(string entryId)
        {
            CopiedAgain.Add(entryId);
            return Task.FromResult(true);
        }
    }
}
