using Dynasland.Core.Activities;
using Dynasland.Core.Layout;
using Dynasland.Core.Models;
using Dynasland.Modules.Media;
using Microsoft.Extensions.Time.Testing;

namespace Dynasland.Tests.Modules;

/// <summary>Música e filme ao mesmo tempo: cada mídia vira uma aba e o compacto mostra a mais relevante.</summary>
public class MultipleMediaTests : IDisposable
{
    private const string Spotify = "Spotify.exe";
    private const string Opera = "opera.exe";

    private readonly FakeMediaService _media = new();
    private readonly ActivityRecorder _recorder = new();
    private readonly FakeTimeProvider _time = new();
    private readonly ModuleOptionsServiceAccessor _options = new();
    private readonly MediaModule _module;

    public MultipleMediaTests() =>
        _module = new MediaModule(_media, _recorder.Bus, new InlineDispatcher(), _time, _options.Service, new FakeIslandSettings());

    public void Dispose() => _recorder.Dispose();

    private static MediaSnapshot Music(string title, MediaPlaybackState state = MediaPlaybackState.Playing) =>
        FakeMediaService.Track(title, state, Spotify);

    private static MediaSnapshot Movie(string title, MediaPlaybackState state = MediaPlaybackState.Playing) =>
        FakeMediaService.Track(title, state, Opera, MediaKind.Unknown);

    private IslandActivity LastPosted(string sessionId) =>
        _recorder.PostedWithId(MediaModule.SessionActivityId(sessionId)).Last();

    [Fact]
    public async Task Each_session_becomes_its_own_tab_with_priority_by_relevance()
    {
        _media.SetSessions(Spotify, Music("Grown Woman"), Movie("Filme", MediaPlaybackState.Paused));

        await _module.StartAsync(CancellationToken.None);

        Assert.Equal(ActivityPriority.High, LastPosted(Spotify).Priority);
        Assert.Equal(ActivityPriority.Low, LastPosted(Opera).Priority);
        Assert.NotSame(LastPosted(Spotify).Content, LastPosted(Opera).Content);
    }

    [Fact]
    public async Task Both_playing_keeps_windows_current_session_on_top()
    {
        _media.SetSessions(Spotify, Music("Música"), Movie("Filme"));
        await _module.StartAsync(CancellationToken.None);

        Assert.Equal(ActivityPriority.High, LastPosted(Spotify).Priority);
        Assert.Equal(ActivityPriority.Normal, LastPosted(Opera).Priority);

        // O usuário dá play no filme e o Windows passa a considerá-lo a sessão atual.
        _media.RaiseSessions(Opera, Movie("Filme"), Music("Música", MediaPlaybackState.Paused));

        Assert.Equal(ActivityPriority.High, LastPosted(Opera).Priority);
        Assert.Equal(ActivityPriority.Low, LastPosted(Spotify).Priority);
    }

    [Fact]
    public async Task Coordinator_shows_the_most_relevant_media_and_offers_the_other_as_a_page()
    {
        var coordinator = new IslandCoordinator(() => IslandSettings.Default);
        using var forward = _recorder.Bus.Subscribe<ActivityPostedEvent>(e => coordinator.Post(e.Activity, _time.GetUtcNow()));
        _media.SetSessions(Spotify, Music("Música"), Movie("Filme", MediaPlaybackState.Paused));
        await _module.StartAsync(CancellationToken.None);

        Assert.Equal(MediaModule.SessionActivityId(Spotify), coordinator.Snapshot.Indicator?.Id);

        coordinator.ToggleInteractive(_time.GetUtcNow());
        Assert.Equal(2, coordinator.Snapshot.PageCount);
        coordinator.Cycle(1, _time.GetUtcNow());
        Assert.Equal(MediaModule.SessionActivityId(Opera), coordinator.Snapshot.Focused?.Id);
    }

    [Fact]
    public async Task Compact_indicator_shows_how_many_other_media_exist()
    {
        _media.SetSessions(Spotify, Music("Música"), Movie("Filme"));
        await _module.StartAsync(CancellationToken.None);

        var content = Assert.IsType<MediaContent>(LastPosted(Spotify).Content);
        Assert.Equal("+1", content.OtherSessionsText);
        Assert.Equal(252, content.GetPreferredDimensions(IslandState.Compact)!.Width);

        _media.RaiseSessions(Spotify, Music("Música"));
        Assert.False(content.HasOtherSessions);
        Assert.Contains(MediaModule.SessionActivityId(Opera), _recorder.Removed);
    }

    [Fact]
    public async Task Controls_act_on_their_own_session()
    {
        _media.SetSessions(Spotify, Music("Música"), Movie("Filme"));
        await _module.StartAsync(CancellationToken.None);

        await Assert.IsType<MediaContent>(LastPosted(Opera).Content).PlayPauseCommand.ExecuteAsync(null);

        Assert.Equal([Opera], _media.Paused);
    }

    [Fact]
    public async Task Videos_are_not_announced_by_default_but_music_is()
    {
        await _module.StartAsync(CancellationToken.None);

        _media.RaiseSessions(Opera, Movie("Episódio 1"));
        Assert.Empty(_recorder.PostedWithId(MediaModule.TrackActivityId));

        _media.RaiseSessions(Spotify, Music("Nova música"), Movie("Episódio 1"));
        Assert.Single(_recorder.PostedWithId(MediaModule.TrackActivityId));

        await _options.Service.SetAsync(MediaModule.ModuleId, MediaModule.AnnounceVideos, 1);
        _media.RaiseSessions(Spotify, Music("Nova música"), Movie("Episódio 2"));
        Assert.Equal(2, _recorder.PostedWithId(MediaModule.TrackActivityId).Count());
    }

    [Fact]
    public async Task Long_paused_media_leaves_the_island_and_returns_when_played()
    {
        _media.SetSessions(Spotify, Music("Música"), Movie("Filme", MediaPlaybackState.Paused));
        await _module.StartAsync(CancellationToken.None);

        _time.Advance(TimeSpan.FromMinutes(9));
        Assert.DoesNotContain(MediaModule.SessionActivityId(Opera), _recorder.Removed);

        _time.Advance(TimeSpan.FromMinutes(1.5));
        Assert.Contains(MediaModule.SessionActivityId(Opera), _recorder.Removed);
        Assert.DoesNotContain(MediaModule.SessionActivityId(Spotify), _recorder.Removed);

        _media.RaiseSessions(Opera, Movie("Filme"), Music("Música"));
        Assert.Equal(ActivityPriority.High, LastPosted(Opera).Priority);
    }

    [Theory]
    [InlineData("opera.exe", "Opera Internet Browser", MediaKind.Unknown, true)]
    [InlineData("Chrome", "Google Chrome", MediaKind.Unknown, true)]
    [InlineData("308046B0AF4A39CB", "Firefox", MediaKind.Unknown, true)]
    [InlineData("Spotify.exe", "Spotify", MediaKind.Unknown, false)]
    [InlineData("Spotify.exe", "Spotify", MediaKind.Video, true)]
    [InlineData("Chrome", "Google Chrome", MediaKind.Music, false)]
    [InlineData("opera.exe", "Opera GX Internet Browser", MediaKind.Music, true, "")]
    [InlineData("Spotify.exe", "Spotify", MediaKind.Music, false, "")]
    public void Browser_media_of_unknown_type_is_treated_as_video(string appId, string appName, MediaKind kind, bool expected, string artist = "Artista")
    {
        var snapshot = FakeMediaService.Track("x", app: appId, kind: kind) with { AppName = appName, Artist = artist };

        Assert.Equal(expected, MediaModule.IsLikelyVideo(snapshot));
    }

    private sealed class ModuleOptionsServiceAccessor
    {
        public Dynasland.Core.Modules.ModuleOptionsService Service { get; } = TestOptions.Create();
    }
}
