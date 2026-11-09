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
    private readonly NotificationPreferences _preferences;
    private readonly NotificationsModule _module;

    public NotificationsModuleTests()
    {
        _preferences = new NotificationPreferences(_store);
        _module = new NotificationsModule(_service, _preferences, TestOptions.Create(), _recorder.Bus, NullLogger<NotificationsModule>.Instance);
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
