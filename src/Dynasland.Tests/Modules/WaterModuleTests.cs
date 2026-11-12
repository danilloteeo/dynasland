using Dynasland.App.ViewModels.Hub;
using Dynasland.Core.Activities;
using Dynasland.Core.Interfaces;
using Dynasland.Core.Models;
using Dynasland.Core.Modules;
using Dynasland.Core.Shortcuts;
using Dynasland.Infrastructure.Repositories;
using Dynasland.Modules.Water;
using Dynasland.Tests.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace Dynasland.Tests.Modules;

public class WaterModuleTests : IDisposable
{
    // 08:30 UTC; FakeTimeProvider usa UTC como fuso local.
    private static readonly DateTimeOffset T0 = new(2026, 11, 11, 8, 30, 0, TimeSpan.Zero);
    private readonly ActivityRecorder _recorder = new();
    private readonly TemporaryDatabase _db = new();

    public void Dispose()
    {
        _recorder.Dispose();
        _db.Dispose();
    }

    private sealed record Fixture(WaterModule Module, SqliteWaterStore Store, FakeTimeProvider Time, GlanceRegistry Glances, ModuleOptionsService Options, ShortcutRegistry Shortcuts);

    private async Task<Fixture> StartAsync(DateTimeOffset now, Func<SqliteWaterStore, IWaterStore>? wrap = null, IDockBadges? badges = null)
    {
        await _db.CreateRunner().MigrateAsync();
        var store = new SqliteWaterStore(_db.Factory);
        var time = new FakeTimeProvider(now);
        var glances = new GlanceRegistry();
        var options = TestOptions.Create();
        var shortcuts = new ShortcutRegistry();
        var module = new WaterModule(wrap?.Invoke(store) ?? store, _recorder.Bus, new InlineDispatcher(), time, shortcuts, glances, new SilentSound(),
            options, new GlobalShortcutRegistry(new InMemorySettingsStore()), NullLogger<WaterModule>.Instance, badges);
        await module.StartAsync(CancellationToken.None);
        await module.RescheduleAsync();
        return new Fixture(module, store, time, glances, options, shortcuts);
    }

    private List<WaterReminderContent> Reminders => [.. _recorder.Posted.Select(a => a.Content).OfType<WaterReminderContent>()];

    private List<WaterDrankContent> Confirmations => [.. _recorder.Posted.Select(a => a.Content).OfType<WaterDrankContent>()];

    private static readonly DateOnly Today = new(2026, 11, 11);

    [Fact]
    public async Task The_dock_icon_gets_a_progress_ring_that_follows_the_glasses_of_the_day()
    {
        var badges = new DockBadgeRegistry();
        var f = await StartAsync(T0, badges: badges);
        var goal = (int)f.Options.Get(WaterModule.ModuleId, WaterModule.Goal);
        Assert.Equal(new DockBadge(DockBadgeKind.Ring, 0), badges.Badges[WaterModule.ModuleId]);

        await f.Module.DrinkAsync();
        await f.Module.RescheduleAsync();

        var ring = badges.Badges[WaterModule.ModuleId];
        Assert.Equal(DockBadgeKind.Ring, ring.Kind);
        Assert.Equal(1.0 / goal, ring.Progress, precision: 6);

        await f.Module.StopAsync(CancellationToken.None);
        Assert.Empty(badges.Badges);
    }

    [Fact]
    public async Task Island_shortcut_opens_the_body_panel_without_counting_a_glass()
    {
        var f = await StartAsync(T0);
        var shortcut = Assert.Single(f.Shortcuts.Shortcuts, s => s.ModuleId == WaterModule.ModuleId);
        Assert.Equal(WaterModule.PanelActivityId, shortcut.Id);
        Assert.Equal("Água", shortcut.Label);

        shortcut.Invoke();

        var posted = _recorder.Posted[^1];
        Assert.Equal(WaterModule.PanelActivityId, posted.Id);
        Assert.Equal(ActivityKind.Transient, posted.Kind);
        Assert.True(posted.OpenInteractive);
        var panel = Assert.IsType<WaterBodyPanelContent>(posted.Content);
        Assert.Equal(0, panel.Glasses);
        Assert.Equal(8, panel.Goal);
        Assert.Empty(Confirmations);
        await Task.Delay(50);
        Assert.Equal(0, await f.Store.GetAsync(Today));
        await f.Module.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Panel_follows_drinks_undo_and_goal_changes()
    {
        var f = await StartAsync(T0);
        f.Module.OpenPanel();
        var panel = Assert.IsType<WaterBodyPanelContent>(_recorder.Posted[^1].Content);
        Assert.Equal("0 de 8 copos hoje", panel.ProgressText);
        Assert.Equal("0%", panel.PercentText);
        Assert.False(panel.UndoCommand.CanExecute(null));

        await f.Module.DrinkAsync();
        await f.Module.DrinkAsync();
        panel.DrinkCommand.Execute(null);
        await WaitUntil(() => panel.Glasses == 3);
        Assert.Equal(3, await f.Store.GetAsync(Today));
        Assert.Equal(8, panel.Goal);
        Assert.Equal(0.375, panel.Progress, 3);
        Assert.Equal("38%", panel.PercentText);
        Assert.Equal("3 de 8 copos hoje", panel.ProgressText);
        Assert.Equal("Faltam 5 copos para a meta.", panel.Message);
        Assert.False(panel.GoalReached);
        Assert.True(panel.UndoCommand.CanExecute(null));

        panel.UndoCommand.Execute(null);
        await WaitUntil(() => panel.Glasses == 2);
        Assert.Equal(2, await f.Store.GetAsync(Today));
        Assert.Equal(0.25, panel.Progress, 3);

        await f.Options.SetAsync(WaterModule.ModuleId, WaterModule.Goal, 3);
        await WaitUntil(() => panel.Goal == 3);
        Assert.Equal("Falta 1 copo para a meta.", panel.Message);

        await f.Options.SetAsync(WaterModule.ModuleId, WaterModule.Goal, 1);
        await WaitUntil(() => panel.Goal == 1);
        Assert.Equal(1, panel.Progress, 3);
        Assert.Equal("100%", panel.PercentText);
        Assert.Equal("2 de 1 copos hoje", panel.ProgressText);
        Assert.Equal("Meta de hoje batida!", panel.Message);
        Assert.True(panel.GoalReached);
        await f.Module.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task With_the_panel_open_drinking_does_not_cover_it_with_the_confirmation()
    {
        var f = await StartAsync(T0);
        f.Module.OpenPanel();
        var panel = Assert.IsType<WaterBodyPanelContent>(_recorder.Posted[^1].Content);
        panel.OnPresentationChanged(IslandState.Interactive);

        await f.Module.DrinkAsync();
        Assert.Equal(1, await f.Store.GetAsync(Today));
        Assert.Empty(Confirmations);

        // A ilha recolheu: o painel saiu de cena e a confirmação volta a aparecer.
        panel.OnPresentationChanged(null);
        await f.Module.DrinkAsync();
        Assert.Single(Confirmations);
        await f.Module.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Dismissing_the_panel_brings_back_the_confirmation()
    {
        var f = await StartAsync(T0);
        f.Module.OpenPanel();
        var activity = _recorder.Posted[^1];
        await f.Module.DrinkAsync();
        Assert.Empty(Confirmations);

        _recorder.Bus.Publish(new ActivityDismissedEvent(activity));
        await f.Module.DrinkAsync();

        Assert.Single(Confirmations);
        Assert.Equal(2, await f.Store.GetAsync(Today));
        await f.Module.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Reminds_at_the_next_slot_with_progress_and_shows_the_glance()
    {
        var f = await StartAsync(T0);

        Assert.Equal("0/8 copos", Assert.Single(f.Glances.Glances).Text);
        Assert.Empty(Reminders);

        f.Time.Advance(TimeSpan.FromMinutes(30));
        await WaitUntil(() => Reminders.Count == 1);

        Assert.Equal("0 de 8 copos hoje", Reminders[0].Message);
        var posted = _recorder.Posted.Single(a => a.Content is WaterReminderContent);
        Assert.Equal(ActivityPriority.High, posted.Priority);
        Assert.True(posted.OpenInteractive);
        await f.Module.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Drinking_counts_a_glass_updates_the_glance_and_confirms()
    {
        var f = await StartAsync(T0);
        f.Time.Advance(TimeSpan.FromMinutes(30));
        await WaitUntil(() => Reminders.Count == 1);

        Reminders[0].DrinkCommand.Execute(null);

        await WaitUntil(() => f.Glances.Glances.Single().Text == "1/8 copos");
        var posted = _recorder.Posted[^1];
        var confirmation = Assert.IsType<WaterDrankContent>(posted.Content);
        Assert.Equal("Boa!", confirmation.Title);
        Assert.Equal("1 de 8 copos hoje", confirmation.Message);
        Assert.Equal(0, confirmation.FromProgress, 3);
        Assert.Equal(0.125, confirmation.ToProgress, 3);
        Assert.False(confirmation.GoalReached);
        Assert.Equal("water.reminder", posted.Id);
        Assert.Equal(ActivityKind.Transient, posted.Kind);
        Assert.Equal(TimeSpan.FromSeconds(5), posted.Duration);
        Assert.False(posted.OpenInteractive);
        Assert.Equal(1, await f.Store.GetAsync(new DateOnly(2026, 11, 11)));
        await f.Module.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Snooze_repeats_the_reminder_after_ten_minutes_without_changing_the_count()
    {
        var f = await StartAsync(T0);
        f.Time.Advance(TimeSpan.FromMinutes(30));
        await WaitUntil(() => Reminders.Count == 1);

        Reminders[0].SnoozeCommand.Execute(null);
        await Task.Delay(50);
        f.Time.Advance(TimeSpan.FromMinutes(9));
        await Task.Delay(50);
        Assert.Single(Reminders);

        f.Time.Advance(TimeSpan.FromMinutes(1));
        await WaitUntil(() => Reminders.Count == 2);
        Assert.Equal(0, await f.Store.GetAsync(new DateOnly(2026, 11, 11)));
        await f.Module.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Reaching_the_goal_stops_the_reminders_for_the_day_and_congratulates()
    {
        var f = await StartAsync(T0);
        await f.Options.SetAsync(WaterModule.ModuleId, WaterModule.Goal, 1);

        await f.Module.DrinkAsync();

        var congrats = Assert.IsType<WaterDrankContent>(_recorder.Posted[^1].Content);
        Assert.Equal("Meta de água batida!", congrats.Title);
        Assert.Equal("1 de 1 copos hoje", congrats.Message);
        Assert.Equal(0, congrats.FromProgress, 3);
        Assert.Equal(1, congrats.ToProgress, 3);
        Assert.True(congrats.GoalReached);
        f.Time.Advance(TimeSpan.FromHours(3));
        await Task.Delay(100);
        Assert.Empty(Reminders);
        await f.Module.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task The_count_restarts_on_the_next_day()
    {
        // Fora do horário ativo o próximo aviso é só às 08:00; a virada do dia precisa agendar o glance sozinha.
        var f = await StartAsync(T0.AddHours(15)); // 23:30
        await f.Module.DrinkAsync();
        await WaitUntil(() => f.Glances.Glances.Single().Text == "1/8 copos");

        f.Time.Advance(TimeSpan.FromMinutes(30) + TimeSpan.FromSeconds(1)); // 00:00:01 do dia seguinte

        await WaitUntil(() => f.Glances.Glances.Single().Text == "0/8 copos");
        await f.Module.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Invalid_active_window_falls_back_to_the_defaults()
    {
        var f = await StartAsync(T0);
        await f.Options.SetAsync(WaterModule.ModuleId, WaterModule.StartHour, 20);
        await f.Options.SetAsync(WaterModule.ModuleId, WaterModule.EndHour, 10);
        await f.Module.RescheduleAsync();

        f.Time.Advance(TimeSpan.FromMinutes(30)); // 09:00 está no horário padrão 8–22
        await WaitUntil(() => Reminders.Count == 1);
        await f.Module.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Overlapping_reschedules_keep_the_timer_of_the_final_settings()
    {
        GatedStore? gated = null;
        var f = await StartAsync(T0, s => gated = new GatedStore(s));
        gated!.ArmNext();

        // A primeira mudança fica presa na leitura do store; a segunda termina antes dela no código sem serialização.
        await f.Options.SetAsync(WaterModule.ModuleId, WaterModule.StartHour, 20);
        await gated.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await f.Options.SetAsync(WaterModule.ModuleId, WaterModule.StartHour, 8);
        await Task.Delay(100);
        gated.Release.SetResult();
        await Task.Delay(200);

        f.Time.Advance(TimeSpan.FromMinutes(30)); // 09:00 vale para o início final (8h), não para 20h
        await WaitUntil(() => Reminders.Count == 1);
        await f.Module.StopAsync(CancellationToken.None);
    }

    private sealed class GatedStore(IWaterStore inner) : IWaterStore
    {
        private int _armed;

        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public event EventHandler? Changed
        {
            add => inner.Changed += value;
            remove => inner.Changed -= value;
        }

        public void ArmNext() => Interlocked.Exchange(ref _armed, 1);

        public async Task<int> GetAsync(DateOnly day, CancellationToken cancellationToken = default)
        {
            if (Interlocked.Exchange(ref _armed, 0) == 1)
            {
                Entered.SetResult();
                await Release.Task;
            }

            return await inner.GetAsync(day, cancellationToken);
        }

        public Task<int> AddAsync(DateOnly day, int delta, CancellationToken cancellationToken = default) =>
            inner.AddAsync(day, delta, cancellationToken);
    }

    private static async Task WaitUntil(Func<bool> condition)
    {
        for (var i = 0; i < 200 && !condition(); i++)
        {
            await Task.Delay(10);
        }

        Assert.True(condition());
    }

    [Fact]
    public async Task Hub_page_shows_progress_and_adjusts_the_count()
    {
        await _db.CreateRunner().MigrateAsync();
        var store = new SqliteWaterStore(_db.Factory);
        var viewModel = new WaterPageViewModel(store, TestOptions.Create(), new InlineDispatcher(), new FakeTimeProvider(T0), NullLogger<WaterPageViewModel>.Instance);

        Assert.False(viewModel.HasLoaded);
        await viewModel.LoadAsync();
        Assert.True(viewModel.HasLoaded);
        Assert.Equal("0 de 8 copos", viewModel.ProgressText);
        Assert.Equal(0, viewModel.Progress);
        Assert.Equal(0, viewModel.Glasses);
        Assert.Equal(8, viewModel.Goal);

        await viewModel.AddCommand.ExecuteAsync(null);
        await viewModel.AddCommand.ExecuteAsync(null);
        await viewModel.RemoveCommand.ExecuteAsync(null);

        Assert.Equal("1 de 8 copos", viewModel.ProgressText);
        Assert.Equal(1 / 8d, viewModel.Progress);
        Assert.Equal(1, viewModel.Glasses);
        Assert.Equal(8, viewModel.Goal);
        Assert.Equal("Faltam 7 copos para a meta.", viewModel.Message);
        viewModel.Dispose();
    }

    private sealed class NoHub : IHubService
    {
        public void Open(HubPage page)
        {
        }
    }
}
