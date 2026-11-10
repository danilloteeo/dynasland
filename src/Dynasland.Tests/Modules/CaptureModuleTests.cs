using Dynasland.Core.Activities;
using Dynasland.Core.Interfaces;
using Dynasland.Core.Models;
using Dynasland.Core.Modules;
using Dynasland.Core.Shortcuts;
using Dynasland.Infrastructure.Repositories;
using Dynasland.Infrastructure.Windows.Capture;
using Dynasland.Modules.Capture;
using Dynasland.Tests.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace Dynasland.Tests.Modules;

[Collection("CaptureOutput")]
public class CaptureModuleTests : IDisposable
{
    private readonly ActivityRecorder _recorder = new();
    private readonly string _root = Path.Combine(Path.GetTempPath(), "dynasland-tests", Guid.NewGuid().ToString("N"));
    private readonly TemporaryDatabase _db = new();

    public CaptureModuleTests() => CaptureModule.OutputRoot = _root;

    public void Dispose()
    {
        CaptureModule.OutputRoot = null;
        _recorder.Dispose();
        _db.Dispose();
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private async Task<(CaptureModule Module, FakeRecorder Recorder, SqliteCaptureHistoryStore History, FakeRegion Region)> CreateAsync()
    {
        await _db.CreateRunner().MigrateAsync();
        var history = new SqliteCaptureHistoryStore(_db.Factory);
        var recorder = new FakeRecorder();
        var region = new FakeRegion();
        var module = new CaptureModule(new FakeScreenshots(), recorder, region, history, new SilentClipboard(), new NoShell(), new FakeMonitors(),
            new NoHub(), _recorder.Bus, new InlineDispatcher(), new FakeTimeProvider(new DateTimeOffset(2026, 11, 10, 15, 0, 0, TimeSpan.Zero)),
            TestOptions.Create(), new ShortcutRegistry(), new GlobalShortcutRegistry(new InMemorySettingsStore()), NullLogger<CaptureModule>.Instance);
        await module.StartAsync(CancellationToken.None);
        return (module, recorder, history, region);
    }

    [Fact]
    public async Task Screen_capture_is_saved_recorded_and_announced()
    {
        var (module, _, history, _) = await CreateAsync();

        await module.CaptureScreenAsync();

        var record = Assert.Single(await history.ListAsync());
        Assert.Equal(CaptureKind.Screenshot, record.Kind);
        Assert.True(File.Exists(record.Path));
        Assert.StartsWith(Path.Combine(_root, "Dynasland"), record.Path);
        var result = Assert.IsType<CaptureResultContent>(Assert.Single(_recorder.PostedWithId(CaptureModule.ResultActivityId)).Content);
        Assert.Contains("4 × 2", result.Detail);
    }

    [Fact]
    public async Task Region_capture_crops_the_frozen_screen_and_cancel_does_nothing()
    {
        var (module, _, history, region) = await CreateAsync();

        region.Selection = null;
        await module.CaptureRegionAsync();
        Assert.Empty(await history.ListAsync());

        region.Selection = new PixelRect(1, 0, 4, 2);
        await module.CaptureRegionAsync();
        Assert.Empty(_recorder.PostedWithId(CaptureModule.StatusActivityId).Select(a => ((StatusContent)a.Content).Message));
        var record = Assert.Single(await history.ListAsync());
        var image = Dynasland.Modules.Common.ImageDecoder.Decode(await File.ReadAllBytesAsync(record.Path), 3) as System.Windows.Media.Imaging.BitmapSource;
        Assert.Equal(3, image!.PixelWidth);
    }

    [Fact]
    public async Task Recording_shows_a_live_activity_that_cannot_be_dismissed_and_saves_history()
    {
        var (module, recorder, history, _) = await CreateAsync();

        await module.ToggleRecordingAsync();
        var live = Assert.Single(_recorder.PostedWithId(CaptureModule.RecordingActivityId));
        Assert.Equal(ActivityKind.Live, live.Kind);
        Assert.False(live.CanDismiss);
        Assert.Equal(30, recorder.Options!.FramesPerSecond);

        await module.ToggleRecordingAsync();
        await WaitUntil(async () => (await history.ListAsync()).Count == 1);
        Assert.Contains(CaptureModule.RecordingActivityId, _recorder.Removed);
        await module.StopAsync(CancellationToken.None);
    }

    private static async Task WaitUntil(Func<Task<bool>> condition)
    {
        for (var i = 0; i < 200 && !await condition(); i++)
        {
            await Task.Delay(10);
        }

        Assert.True(await condition());
    }

    private sealed class FakeScreenshots : IScreenshotService
    {
        // Imagem 4×2 de verdade, para o recorte funcionar.
        private static readonly CapturedImage Image = ScreenshotService.Encode(new byte[4 * 2 * 4], 4, 2);

        public Task<CapturedImage> CaptureScreenAsync(string? monitorDeviceName = null) => Task.FromResult(Image);

        public Task<CapturedImage> CaptureRegionAsync(PixelRect region) => Task.FromResult(Image);

        public Task<CapturedImage> CaptureWindowAsync(IntPtr window) => Task.FromResult(Image);

        public IntPtr GetForegroundWindow() => new(1);

        public IReadOnlyList<CaptureWindowInfo> GetWindows() => [];
    }

    private sealed class FakeRegion : IRegionSelector
    {
        public PixelRect? Selection { get; set; }

        public Task<PixelRect?> SelectAsync(CapturedImage background, PixelRect desktop) => Task.FromResult(Selection);
    }

    private sealed class FakeMonitors : IMonitorService
    {
        public IReadOnlyList<DisplayMonitor> GetMonitors() => [new DisplayMonitor(@"\\.\DISPLAY1", new PixelRect(0, 0, 4, 2), new PixelRect(0, 0, 4, 2), true, 96)];
    }

    private sealed class FakeRecorder : IScreenRecorder
    {
        public RecordingOptions? Options { get; private set; }

        public bool IsSupported => true;

        public RecordingState State { get; private set; }

        public TimeSpan Elapsed => TimeSpan.FromSeconds(3);

        public event EventHandler<RecordingState>? StateChanged;

        public event EventHandler<RecordingResult>? Completed;

        public Task StartAsync(RecordingOptions options, CancellationToken cancellationToken = default)
        {
            Options = options;
            Set(RecordingState.Recording);
            return Task.CompletedTask;
        }

        public void Pause() => Set(RecordingState.Paused);

        public void Resume() => Set(RecordingState.Recording);

        public Task StopAsync()
        {
            if (State == RecordingState.Idle)
            {
                return Task.CompletedTask;
            }

            Set(RecordingState.Idle);
            Directory.CreateDirectory(Path.GetDirectoryName(Options!.OutputPath)!);
            File.WriteAllBytes(Options.OutputPath, [1, 2, 3]);
            Completed?.Invoke(this, new RecordingResult(true, Options.OutputPath, TimeSpan.FromSeconds(3), 3, null));
            return Task.CompletedTask;
        }

        private void Set(RecordingState state)
        {
            State = state;
            StateChanged?.Invoke(this, state);
        }
    }

    private sealed class SilentClipboard : IClipboardService
    {
        public bool IsHistoryEnabled => false;

        public event EventHandler? ContentChanged
        {
            add { }
            remove { }
        }

        public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task StopAsync() => Task.CompletedTask;

        public Task<ClipboardEntry?> GetCurrentAsync() => Task.FromResult<ClipboardEntry?>(null);

        public Task<IReadOnlyList<ClipboardEntry>> GetHistoryAsync(int maxItems) => Task.FromResult<IReadOnlyList<ClipboardEntry>>([]);

        public Task<bool> CopyAgainAsync(string entryId) => Task.FromResult(false);

        public Task<bool> SetTextAsync(string text) => Task.FromResult(true);

        public Task<bool> SetImageAsync(byte[] image) => Task.FromResult(true);
    }

    private sealed class NoShell : IShellService
    {
        public bool Open(string path) => true;

        public bool RevealInFolder(string path) => true;

        public bool OpenUrl(string url) => true;
    }

    private sealed class NoHub : IHubService
    {
        public void Open(HubPage page)
        {
        }
    }
}
