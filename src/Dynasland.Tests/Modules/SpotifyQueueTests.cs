using Dynasland.Core.Interfaces;
using Dynasland.Core.Models;
using Dynasland.Modules.Media;
using Microsoft.Extensions.Time.Testing;

namespace Dynasland.Tests.Modules;

public class SpotifyQueueTests : IDisposable
{
    private readonly ActivityRecorder _recorder = new();

    public void Dispose() => _recorder.Dispose();

    [Fact]
    public async Task Spotify_track_shows_upcoming_queue_and_other_players_do_not()
    {
        var media = new FakeMediaService();
        var spotify = new FakeSpotify();
        var module = new MediaModule(media, _recorder.Bus, new InlineDispatcher(), new FakeTimeProvider(), TestOptions.Create(), new FakeIslandSettings(), spotify);
        await module.StartAsync(CancellationToken.None);

        media.Raise(FakeMediaService.Track("Atual"));
        var content = (MediaContent)_recorder.Posted.First(a => a.Id.StartsWith(MediaModule.SessionActivityPrefix, StringComparison.Ordinal)).Content;
        await WaitUntil(() => content.Queue.Count == 3);

        Assert.Equal(["Um", "Dois", "Três"], content.Queue.Select(q => q.Name));
        Assert.True(content.HasQueue);

        media.Raise(FakeMediaService.Track("Vídeo", app: "opera.exe"));
        Assert.Equal(1, spotify.Requests);
        await module.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Disconnected_spotify_keeps_panel_without_queue()
    {
        var media = new FakeMediaService();
        var module = new MediaModule(media, _recorder.Bus, new InlineDispatcher(), new FakeTimeProvider(), TestOptions.Create(), new FakeIslandSettings(), new FakeSpotify { Connected = false });
        await module.StartAsync(CancellationToken.None);

        media.Raise(FakeMediaService.Track("Atual"));
        var content = (MediaContent)_recorder.Posted.First().Content;
        await Task.Delay(50);

        Assert.False(content.HasQueue);
        await module.StopAsync(CancellationToken.None);
    }

    private static async Task WaitUntil(Func<bool> condition)
    {
        for (var i = 0; i < 200 && !condition(); i++)
        {
            await Task.Delay(10);
        }

        Assert.True(condition());
    }

    private sealed class FakeSpotify : ISpotifyService
    {
        public bool Connected { get; set; } = true;

        public int Requests { get; private set; }

        public string RedirectUri => "http://127.0.0.1:43821/callback";

        public SpotifyConnectionState State => Connected ? SpotifyConnectionState.Connected : SpotifyConnectionState.Disconnected;

        public string? AccountName => "Teste";

        public event EventHandler? Changed
        {
            add { }
            remove { }
        }

        public Task<string?> ConnectAsync(CancellationToken cancellationToken = default) => Task.FromResult<string?>(null);

        public Task DisconnectAsync() => Task.CompletedTask;

        public Task<SpotifyQueue?> GetQueueAsync(CancellationToken cancellationToken = default)
        {
            Requests++;
            var upcoming = new[] { "Um", "Dois", "Três", "Quatro" }.Select(n => new SpotifyTrack(n, "Artista", null, TimeSpan.FromMinutes(3))).ToList();
            return Task.FromResult<SpotifyQueue?>(new SpotifyQueue(null, upcoming));
        }

        public Task<byte[]?> GetImageAsync(string url, CancellationToken cancellationToken = default) => Task.FromResult<byte[]?>(null);
    }
}
