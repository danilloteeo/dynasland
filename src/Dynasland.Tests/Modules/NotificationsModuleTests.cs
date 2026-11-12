using Dynasland.Core.Activities;
using Dynasland.Core.Models;
using Dynasland.Core.Modules;
using Dynasland.Modules.Notifications;
using Microsoft.Extensions.Logging.Abstractions;

namespace Dynasland.Tests.Modules;

public class NotificationsModuleTests : IDisposable
{
    private readonly ActivityRecorder _recorder = new();
    private readonly FakeNotificationService _service = new();
    private readonly InMemorySettingsStore _store = new();
    private readonly FakeNotificationHistory _history = new();
    private readonly NotificationPreferences _preferences;
    private readonly NotificationsModule _module;

    public NotificationsModuleTests()
    {
        _preferences = new NotificationPreferences(_store);
        _module = new NotificationsModule(_service, _preferences, _history, new FakeIslandSettings(), TestOptions.Create(), _recorder.Bus, NullLogger<NotificationsModule>.Instance);
    }

    public void Dispose() => _recorder.Dispose();

    [Fact]
    public async Task Received_notification_becomes_transient_activity()
    {
        await _module.StartAsync(CancellationToken.None);

        _service.Receive(FakeNotificationService.Message(7));

        var activity = Assert.Single(_recorder.Posted);
        Assert.Equal("notification.7", activity.Id);
        Assert.Equal(ActivityKind.Transient, activity.Kind);
        var content = Assert.IsType<NotificationContent>(activity.Content);
        Assert.Equal("Maria", content.Headline);
        Assert.Equal("Oi!", content.Body);
    }

    [Fact]
    public async Task Per_app_rules_control_island_history_priority_and_duration()
    {
        await _module.StartAsync(CancellationToken.None);
        var message = FakeNotificationService.Message(1);
        await _preferences.SetHighPriorityAsync(message.AppId, true);
        await _preferences.SetAppDurationAsync(message.AppId, 12);

        _service.Receive(message);
        var shown = Assert.Single(_recorder.Posted);
        Assert.Equal(ActivityPriority.High, shown.Priority);
        Assert.Equal(NotificationReading.Duration(TimeSpan.FromSeconds(12), message.Body.Length, highPriority: true), shown.Duration);
        Assert.Equal($"notification.{message.AppId}", shown.GroupKey);

        await _preferences.SetHistoryOnlyAsync(message.AppId, true);
        _service.Receive(FakeNotificationService.Message(2));
        Assert.Single(_recorder.Posted);

        await _preferences.SetBlockedAsync(message.AppId, true);
        _service.Receive(FakeNotificationService.Message(3));

        await Task.Delay(50);
        Assert.Equal([true, false], _history.Records.Select(r => r.WasShown));
    }

    [Fact]
    public async Task Card_carries_duration_app_line_and_single_line_body()
    {
        await _module.StartAsync(CancellationToken.None);
        await _preferences.SetAppDurationAsync("Teams", 12);

        _service.Receive(FakeNotificationService.Message(1, body: "Oi!\r\n\nTudo bem?"));
        _service.Receive(FakeNotificationService.Message(2, appId: "Teams"));

        var first = (NotificationContent)_recorder.Posted[0].Content;
        var second = (NotificationContent)_recorder.Posted[1].Content;
        var baseDuration = TimeSpan.FromSeconds(NotificationPreferences.DefaultDurationSeconds);
        Assert.Equal(NotificationReading.Duration(baseDuration, first.Info.Body.Length, false), first.Duration);
        Assert.Equal(NotificationReading.Duration(TimeSpan.FromSeconds(12), second.Info.Body.Length, false), second.Duration);
        Assert.Equal("Oi! Tudo bem?", first.ExpandedBody);
        Assert.Equal($"WhatsApp · {first.TimeText}", first.AppLine);
        Assert.NotSame(first.Countdown, second.Countdown);
    }

    [Fact]
    public async Task Without_logo_the_card_stays_neutral()
    {
        await _module.StartAsync(CancellationToken.None);

        _service.Receive(FakeNotificationService.Message(1));

        var content = (NotificationContent)_recorder.Posted.Single().Content;
        Assert.False(content.HasAccent);
        Assert.Equal(0, content.GlowColor.A);
    }

    [Fact]
    public async Task Rules_survive_reload()
    {
        await _preferences.SetHistoryOnlyAsync("app", true);
        await _preferences.SetHighPriorityAsync("app", true);
        await _preferences.SetAppDurationAsync("app", 9);

        var reloaded = new NotificationPreferences(_store);
        await reloaded.LoadAsync();

        var decision = reloaded.Decide("app");
        Assert.Equal(NotificationVisibility.HistoryOnly, decision.Visibility);
        Assert.True(decision.HighPriority);
        Assert.Equal(TimeSpan.FromSeconds(9), decision.Duration);
    }

    [Fact]
    public async Task Notification_without_title_uses_app_name_as_headline()
    {
        await _module.StartAsync(CancellationToken.None);

        _service.Receive(FakeNotificationService.Message(1, appId: "Outlook", title: null));

        Assert.Equal("Outlook", Assert.IsType<NotificationContent>(_recorder.Posted.Single().Content).Headline);
    }

    [Fact]
    public async Task Removed_in_windows_is_removed_from_island()
    {
        await _module.StartAsync(CancellationToken.None);

        _service.Remove(7);

        Assert.Equal(["notification.7"], _recorder.Removed);
    }

    [Fact]
    public async Task Dismiss_and_open_act_on_windows_and_leave_the_island()
    {
        await _module.StartAsync(CancellationToken.None);
        _service.Receive(FakeNotificationService.Message(1));
        _service.Receive(FakeNotificationService.Message(2, appId: "Teams"));
        var first = (NotificationContent)_recorder.Posted[0].Content;
        var second = (NotificationContent)_recorder.Posted[1].Content;

        first.DismissCommand.Execute(null);
        second.OpenCommand.Execute(null);

        Assert.Equal([1u], _service.Dismissed);
        Assert.Equal(["Teams"], _service.Opened);
        Assert.Equal(["notification.1", "notification.2"], _recorder.Removed);
    }

    [Fact]
    public async Task Muting_an_app_persists_and_filters_future_notifications()
    {
        await _module.StartAsync(CancellationToken.None);
        _service.Receive(FakeNotificationService.Message(1, appId: "Discord"));

        ((NotificationContent)_recorder.Posted[0].Content).MuteAppCommand.Execute(null);
        _service.Receive(FakeNotificationService.Message(2, appId: "discord"));
        _service.Receive(FakeNotificationService.Message(3, appId: "WhatsApp"));

        Assert.Equal(["notification.1", "notification.3"], _recorder.Posted.Select(a => a.Id));
        Assert.Equal("Discord\t0", _store.Values[NotificationPreferences.RulesKey]);

        var reloaded = new NotificationPreferences(_store);
        await reloaded.LoadAsync();
        Assert.True(reloaded.IsBlocked("DISCORD"));
    }

    [Theory]
    [InlineData(NotificationAccess.Denied, "Privacidade")]
    [InlineData(NotificationAccess.Unavailable, "indisponível")]
    public async Task Without_access_the_module_is_unavailable_with_guidance(NotificationAccess access, string hint)
    {
        _service.Access = access;

        var error = await Assert.ThrowsAsync<ModuleUnavailableException>(() => _module.StartAsync(CancellationToken.None));

        Assert.Contains(hint, error.Message);
    }
}

internal sealed class FakeNotificationHistory : Dynasland.Core.Interfaces.INotificationHistoryStore
{
    public List<NotificationRecord> Records { get; } = [];

    public Task AddAsync(NotificationRecord record, CancellationToken cancellationToken = default)
    {
        lock (Records)
        {
            Records.Add(record);
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<NotificationRecord>> ListAsync(int limit = 300, string? appId = null, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<NotificationRecord>>([.. Records]);

    public Task DeleteAsync(long id, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task DeleteOlderThanAsync(DateTimeOffset cutoff, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task ClearAsync(CancellationToken cancellationToken = default)
    {
        Records.Clear();
        return Task.CompletedTask;
    }
}
