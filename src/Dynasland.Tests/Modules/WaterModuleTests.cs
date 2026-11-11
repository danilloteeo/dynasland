using Dynasland.App.ViewModels.Hub;
using Dynasland.Core.Activities;
using Dynasland.Core.Interfaces;
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

    private sealed record Fixture(WaterModule Module, SqliteWaterStore Store, FakeTimeProvider Time, GlanceRegistry Glances, ModuleOptionsService Options);

    private async Task<Fixture> StartAsync(DateTimeOffset now, Func<SqliteWaterStore, IWaterStore>? wrap = null)
    {
        await _db.CreateRunner().MigrateAsync();
        var store = new SqliteWaterStore(_db.Factory);
        var time = new FakeTimeProvider(now);
        var glances = new GlanceRegistry();
        var options = TestOptions.Create();
        var module = new WaterModule(wrap?.Invoke(store) ?? store, new NoHub(), _recorder.Bus, time, new ShortcutRegistry(), glances, new SilentSound(),
            options, new GlobalShortcutRegistry(new InMemorySettingsStore()), NullLogger<WaterModule>.Instance);
        await module.StartAsync(CancellationToken.None);
        await module.RescheduleAsync();
        return new Fixture(module, store, time, glances, options);
    }

    private List<WaterReminderContent> Reminders => [.. _recorder.Posted.Select(a => a.Content).OfType<WaterReminderContent>()];

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
        var confirmation = Assert.IsType<StatusContent>(_recorder.Posted[^1].Content);
        Assert.Equal("1 de 8 copos hoje", confirmation.Message);
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

        var congrats = Assert.IsType<StatusContent>(_recorder.Posted[^1].Content);
        Assert.Equal("Meta de água batida!", congrats.Title);
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
