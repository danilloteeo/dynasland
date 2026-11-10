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
        var time = new FakeTimeProvider();
        var module = new MediaModule(media, _recorder.Bus, new InlineDispatcher(), time, TestOptions.Create(), new FakeIslandSettings(), spotify);
        await module.StartAsync(CancellationToken.None);

        media.Raise(FakeMediaService.Track("Atual"));
        var content = (MediaContent)_recorder.Posted.First(a => a.Id.StartsWith(MediaModule.SessionActivityPrefix, StringComparison.Ordinal)).Content;
        await WaitUntil(() => content.Queue.Count == 3);

        Assert.Equal(["Um", "Dois", "Três"], content.Queue.Select(q => q.Name));
        Assert.True(content.HasQueue);
        Assert.Equal(4, content.QueueTotal);

        // Clicar na terceira faixa avança três vezes pelos controles de mídia (funciona sem Premium).
        content.Queue[2].Select.Execute(null);
        await WaitUntil(() =>
        {
            time.Advance(TimeSpan.FromMilliseconds(400));
            return media.SkipNextCount == 3;
        });

        media.Raise(FakeMediaService.Track("Vídeo", app: "opera.exe"));
        Assert.Equal(1, spotify.Requests);
        await module.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Spotify_panel_controls_the_volume_inside_spotify_and_warns_when_muted()
    {
        var media = new FakeMediaService();
        var spotify = new FakeSpotify { Volume = 0 };
        var module = new MediaModule(media, _recorder.Bus, new InlineDispatcher(), new FakeTimeProvider(), TestOptions.Create(), new FakeIslandSettings(), spotify);
        await module.StartAsync(CancellationToken.None);
        media.Raise(FakeMediaService.Track("Atual"));
        var content = (MediaContent)_recorder.Posted.First(a => a.Id.StartsWith(MediaModule.SessionActivityPrefix, StringComparison.Ordinal)).Content;

        content.OnPresentationChanged(Dynasland.Core.Models.IslandState.Interactive);
        await WaitUntil(() => content.HasAppVolume);

        Assert.Equal("Volume do Spotify", content.VolumeLabel);
        Assert.Equal(0, content.AppVolume);
        Assert.Contains("0%", content.VolumeMessage);

        content.AppVolume = 65;
        await WaitUntil(() => spotify.VolumesSet.Contains(65));
        await WaitUntil(() => content.VolumeMessage is null);
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

        public bool CanControlPlayback => true;

        public int Volume { get; set; }

        public List<int> VolumesSet { get; } = [];

        public Task<int?> GetVolumeAsync(CancellationToken cancellationToken = default) => Task.FromResult<int?>(Volume);

        public Task<string?> SetVolumeAsync(int percent, CancellationToken cancellationToken = default)
        {
            lock (VolumesSet)
            {
                VolumesSet.Add(percent);
            }

            Volume = percent;
            return Task.FromResult<string?>(null);
        }
    }
}
