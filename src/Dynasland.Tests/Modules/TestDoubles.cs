using Dynasland.Core.Activities;
using Dynasland.Core.Events;
using Dynasland.Core.Interfaces;
using Dynasland.Core.Models;
using Microsoft.Extensions.Logging.Abstractions;

namespace Dynasland.Tests.Modules;

internal sealed class InlineDispatcher : IUiDispatcher
{
    public int Posted { get; private set; }

    public void Post(Action action)
    {
        Posted++;
        action();
    }
}

/// <summary>Barramento real que registra as atividades publicadas e removidas.</summary>
internal sealed class ActivityRecorder : IDisposable
{
    private readonly IDisposable[] _subscriptions;

    public ActivityRecorder()
    {
        Bus = new EventBus(NullLogger<EventBus>.Instance);
        _subscriptions =
        [
            Bus.Subscribe<ActivityPostedEvent>(e => Posted.Add(e.Activity)),
            Bus.Subscribe<ActivityRemovedEvent>(e => Removed.Add(e.ActivityId)),
        ];
    }

    public EventBus Bus { get; }

    public List<IslandActivity> Posted { get; } = [];

    public List<string> Removed { get; } = [];

    public IEnumerable<IslandActivity> PostedWithId(string id) => Posted.Where(a => a.Id == id);

    public void Dispose()
    {
        foreach (var subscription in _subscriptions)
        {
            subscription.Dispose();
        }
    }
}

internal sealed class FakeMediaService : IMediaSessionService
{
    public MediaSnapshot? Current { get; set; }

    public int PlayCalls { get; private set; }

    public int PauseCalls { get; private set; }

    public event EventHandler<MediaSnapshot?>? Changed;

    public void Raise(MediaSnapshot? snapshot)
    {
        Current = snapshot;
        Changed?.Invoke(this, snapshot);
    }

    /// <summary>Simula o serviço real, que publica a primeira leitura durante o StartAsync.</summary>
    public MediaSnapshot? PublishOnStart { get; set; }

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (PublishOnStart is { } snapshot)
        {
            Raise(snapshot);
        }

        return Task.CompletedTask;
    }

    public Task StopAsync() => Task.CompletedTask;

    public Task<bool> PlayAsync()
    {
        PlayCalls++;
        return Task.FromResult(true);
    }

    public Task<bool> PauseAsync()
    {
        PauseCalls++;
        return Task.FromResult(true);
    }

    public Task<bool> SkipNextAsync() => Task.FromResult(true);

    public Task<bool> SkipPreviousAsync() => Task.FromResult(true);

    public static MediaSnapshot Track(string title, MediaPlaybackState state = MediaPlaybackState.Playing, string app = "Spotify.exe") =>
        new(app, "Spotify", title, "Artista", "Álbum", null, state, TimeSpan.FromSeconds(30), TimeSpan.FromMinutes(3),
            DateTimeOffset.UnixEpoch, true, true, true, true);
}

internal sealed class FakeAudioService : IAudioVolumeService
{
    public bool HasDevice { get; set; } = true;

    public VolumeState? Current { get; set; } = new(0.5f, false, "Alto-falantes");

    public List<float> LevelsSet { get; } = [];

    public List<bool> MuteSet { get; } = [];

    public event EventHandler<VolumeChangedEventArgs>? Changed;

    public void Raise(VolumeState state, VolumeChangeReason reason)
    {
        Current = state;
        Changed?.Invoke(this, new VolumeChangedEventArgs(state, reason));
    }

    public Task<bool> StartAsync(CancellationToken cancellationToken = default) => Task.FromResult(HasDevice);

    public Task StopAsync() => Task.CompletedTask;

    public void SetLevel(float level) => LevelsSet.Add(level);

    public void SetMuted(bool muted) => MuteSet.Add(muted);
}

internal sealed class FakeBrightnessService : IBrightnessService
{
    public bool Supported { get; set; } = true;

    public int? Current { get; set; } = 60;

    public List<int> Set { get; } = [];

    public event EventHandler<int>? Changed;

    public void Raise(int percent)
    {
        Current = percent;
        Changed?.Invoke(this, percent);
    }

    public Task<bool> StartAsync(CancellationToken cancellationToken = default) => Task.FromResult(Supported);

    public Task StopAsync() => Task.CompletedTask;

    public Task SetAsync(int percent)
    {
        lock (Set)
        {
            Set.Add(percent);
        }

        return Task.CompletedTask;
    }
}

internal sealed class FakePowerService(PowerState initial) : IPowerService
{
    public PowerState Current { get; private set; } = initial;

    public event EventHandler<PowerState>? Changed;

    public void Raise(PowerState state)
    {
        Current = state;
        Changed?.Invoke(this, state);
    }

    public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task StopAsync() => Task.CompletedTask;
}

internal sealed class FakeNotificationService : INotificationService
{
    public NotificationAccess Access { get; set; } = NotificationAccess.Allowed;

    public List<uint> Dismissed { get; } = [];

    public List<string> Opened { get; } = [];

    public event EventHandler<NotificationInfo>? Received;

    public event EventHandler<uint>? Removed;

    public void Receive(NotificationInfo info) => Received?.Invoke(this, info);

    public void Remove(uint id) => Removed?.Invoke(this, id);

    public Task<NotificationAccess> StartAsync(CancellationToken cancellationToken = default) => Task.FromResult(Access);

    public Task StopAsync() => Task.CompletedTask;

    public void Dismiss(uint notificationId) => Dismissed.Add(notificationId);

    public bool TryOpenApp(string appId)
    {
        Opened.Add(appId);
        return true;
    }

    public static NotificationInfo Message(uint id, string appId = "WhatsApp", string? title = "Maria", string body = "Oi!") =>
        new(id, appId, appId, title, body, DateTimeOffset.UnixEpoch, null);
}

internal sealed class InMemorySettingsStore : ISettingsStore
{
    public Dictionary<string, string> Values { get; } = [];

    public Task<IReadOnlyDictionary<string, string>> GetAllAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyDictionary<string, string>>(new Dictionary<string, string>(Values));

    public Task SetManyAsync(IReadOnlyDictionary<string, string> values, CancellationToken cancellationToken = default)
    {
        foreach (var (key, value) in values)
        {
            Values[key] = value;
        }

        return Task.CompletedTask;
    }
}
