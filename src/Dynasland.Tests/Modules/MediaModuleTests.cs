using Dynasland.Core.Activities;
using Dynasland.Core.Models;
using Dynasland.Modules.Media;
using Microsoft.Extensions.Time.Testing;

namespace Dynasland.Tests.Modules;

public class MediaModuleTests : IDisposable
{
    private readonly FakeMediaService _media = new();
    private readonly ActivityRecorder _recorder = new();
    private readonly FakeTimeProvider _time = new();
    private readonly MediaModule _module;

    public MediaModuleTests() => _module = new MediaModule(_media, _recorder.Bus, new InlineDispatcher(), _time);

    public void Dispose() => _recorder.Dispose();

    [Fact]
    public async Task Media_already_playing_at_startup_becomes_indicator_without_announcement()
    {
        _media.Current = FakeMediaService.Track("Música A");

        await _module.StartAsync(CancellationToken.None);

        var session = Assert.Single(_recorder.Posted);
        Assert.Equal(MediaModule.SessionActivityId, session.Id);
        Assert.Equal(ActivityKind.Live, session.Kind);
        Assert.Equal("Música A", Assert.IsType<MediaContent>(session.Content).Title);
    }

    [Fact]
    public async Task First_reading_published_during_start_is_not_announced()
    {
        _media.PublishOnStart = FakeMediaService.Track("Música A");

        await _module.StartAsync(CancellationToken.None);

        Assert.Empty(_recorder.PostedWithId(MediaModule.TrackActivityId));
        Assert.Single(_recorder.PostedWithId(MediaModule.SessionActivityId));
    }

    [Fact]
    public async Task New_track_playing_is_announced_once()
    {
        await _module.StartAsync(CancellationToken.None);

        _media.Raise(FakeMediaService.Track("Música A"));
        _media.Raise(FakeMediaService.Track("Música A") with { Position = TimeSpan.FromSeconds(40) });
        _media.Raise(FakeMediaService.Track("Música B"));

        var announcements = _recorder.PostedWithId(MediaModule.TrackActivityId).ToList();
        Assert.Equal(2, announcements.Count);
        Assert.All(announcements, a => Assert.Equal(ActivityKind.Transient, a.Kind));
        Assert.Single(_recorder.PostedWithId(MediaModule.SessionActivityId));
    }

    [Fact]
    public async Task Track_changed_while_paused_is_announced_when_playback_resumes()
    {
        _media.Current = FakeMediaService.Track("Música A", MediaPlaybackState.Paused);
        await _module.StartAsync(CancellationToken.None);

        _media.Raise(FakeMediaService.Track("Música A", MediaPlaybackState.Playing));
        Assert.Empty(_recorder.PostedWithId(MediaModule.TrackActivityId));

        _media.Raise(FakeMediaService.Track("Música B", MediaPlaybackState.Paused));
        Assert.Empty(_recorder.PostedWithId(MediaModule.TrackActivityId));

        _media.Raise(FakeMediaService.Track("Música B", MediaPlaybackState.Playing));
        Assert.Single(_recorder.PostedWithId(MediaModule.TrackActivityId));
    }

    [Fact]
    public async Task Session_end_removes_activities_and_next_session_is_announced()
    {
        _media.Current = FakeMediaService.Track("Música A");
        await _module.StartAsync(CancellationToken.None);

        _media.Raise(null);
        Assert.Contains(MediaModule.SessionActivityId, _recorder.Removed);

        _media.Raise(FakeMediaService.Track("Música A"));
        Assert.Equal(2, _recorder.PostedWithId(MediaModule.SessionActivityId).Count());
        Assert.Single(_recorder.PostedWithId(MediaModule.TrackActivityId));
    }

    [Fact]
    public async Task Play_pause_command_calls_the_right_action()
    {
        _media.Current = FakeMediaService.Track("Música A");
        await _module.StartAsync(CancellationToken.None);
        var content = Assert.IsType<MediaContent>(_recorder.Posted.Single().Content);

        await content.PlayPauseCommand.ExecuteAsync(null);
        Assert.Equal(1, _media.PauseCalls);

        _media.Raise(FakeMediaService.Track("Música A", MediaPlaybackState.Paused));
        await content.PlayPauseCommand.ExecuteAsync(null);
        Assert.Equal(1, _media.PlayCalls);
    }

    [Fact]
    public async Task Indicator_animates_only_while_playing_and_visible_in_compact()
    {
        _media.Current = FakeMediaService.Track("Música A");
        await _module.StartAsync(CancellationToken.None);
        var content = Assert.IsType<MediaContent>(_recorder.Posted.Single().Content);

        Assert.False(content.IsIndicatorAnimating);
        content.OnPresentationChanged(IslandState.Compact);
        Assert.True(content.IsIndicatorAnimating);

        content.OnPresentationChanged(IslandState.Interactive);
        Assert.False(content.IsIndicatorAnimating);

        content.OnPresentationChanged(IslandState.Compact);
        _media.Raise(FakeMediaService.Track("Música A", MediaPlaybackState.Paused));
        Assert.False(content.IsIndicatorAnimating);
    }

    [Fact]
    public void Position_is_extrapolated_while_playing_and_capped_at_duration()
    {
        var updatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var playing = FakeMediaService.Track("A") with { Position = TimeSpan.FromSeconds(10), PositionUpdatedAt = updatedAt };

        Assert.Equal(TimeSpan.FromSeconds(15), playing.EstimatePosition(updatedAt.AddSeconds(5)));
        Assert.Equal(TimeSpan.FromMinutes(3), playing.EstimatePosition(updatedAt.AddHours(1)));
        Assert.Equal(TimeSpan.FromSeconds(10), (playing with { PlaybackState = MediaPlaybackState.Paused }).EstimatePosition(updatedAt.AddSeconds(5)));
        Assert.Null((playing with { Position = null }).EstimatePosition(updatedAt));
    }
}
