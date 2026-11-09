using Dynasland.Core.Activities;
using Dynasland.Core.Events;
using Dynasland.Core.Interfaces;
using Dynasland.Core.Modules;
using Microsoft.Extensions.Logging.Abstractions;

namespace Dynasland.Tests.Core;

public class ModuleManagerTests
{
    private readonly EventBus _bus = new(NullLogger<EventBus>.Instance);
    private readonly InMemoryModuleStateStore _store = new();

    private ModuleManager CreateManager(params IIslandModule[] modules) =>
        new(modules, _store, _bus, NullLogger<ModuleManager>.Instance);

    [Fact]
    public async Task Starts_modules_enabled_by_default_and_skips_disabled_by_default()
    {
        var on = new FakeModule("on");
        var off = new FakeModule("off", enabledByDefault: false);
        using var manager = CreateManager(on, off);

        await manager.StartAsync();

        Assert.Equal(1, on.StartCount);
        Assert.Equal(0, off.StartCount);
        Assert.False(manager.GetModules().Single(m => m.Id == "off").IsEnabled);
    }

    [Fact]
    public async Task Persisted_state_overrides_default()
    {
        _store.Values["on"] = false;
        var module = new FakeModule("on");
        using var manager = CreateManager(module);

        await manager.StartAsync();

        Assert.Equal(0, module.StartCount);
    }

    [Fact]
    public async Task Failure_in_one_module_is_isolated()
    {
        var broken = new FakeModule("broken") { FailOnStart = true };
        var healthy = new FakeModule("healthy");
        using var manager = CreateManager(broken, healthy);

        await manager.StartAsync();

        var info = manager.GetModules();
        Assert.False(info.Single(m => m.Id == "broken").IsRunning);
        Assert.Equal("falha simulada", info.Single(m => m.Id == "broken").Error);
        Assert.True(info.Single(m => m.Id == "healthy").IsRunning);
    }

    [Fact]
    public async Task Unavailable_module_is_reported_as_unavailable_not_as_failure()
    {
        var battery = new FakeModule("battery") { UnavailableReason = "Este computador não possui bateria." };
        using var manager = CreateManager(battery);

        await manager.StartAsync();

        var info = manager.GetModules().Single();
        Assert.True(info.IsUnavailable);
        Assert.False(info.IsRunning);
        Assert.True(info.IsEnabled);
        Assert.Equal("Este computador não possui bateria.", info.Error);
    }

    [Fact]
    public async Task Disabling_stops_persists_and_removes_activities()
    {
        var module = new FakeModule("media");
        using var manager = CreateManager(module);
        var stopped = new List<string>();
        using var subscription = _bus.Subscribe<ModuleStoppedEvent>(e => stopped.Add(e.ModuleId));
        await manager.StartAsync();

        await manager.SetEnabledAsync("media", false);

        Assert.Equal(1, module.StopCount);
        Assert.False(_store.Values["media"]);
        Assert.Equal(["media"], stopped);
        Assert.False(manager.GetModules().Single().IsRunning);

        await manager.SetEnabledAsync("media", true);
        Assert.Equal(2, module.StartCount);
        Assert.True(manager.GetModules().Single().IsRunning);
    }

    [Fact]
    public async Task Stop_stops_only_running_modules()
    {
        var running = new FakeModule("running");
        var disabled = new FakeModule("disabled", enabledByDefault: false);
        using var manager = CreateManager(running, disabled);
        await manager.StartAsync();

        await manager.StopAsync();

        Assert.Equal(1, running.StopCount);
        Assert.Equal(0, disabled.StopCount);
    }

    [Fact]
    public void Duplicate_ids_are_rejected()
    {
        Assert.Throws<InvalidOperationException>(() => CreateManager(new FakeModule("x"), new FakeModule("x")));
    }

    [Fact]
    public async Task Unknown_module_is_rejected()
    {
        using var manager = CreateManager(new FakeModule("x"));

        await Assert.ThrowsAsync<ArgumentException>(() => manager.SetEnabledAsync("y", true));
    }

    private sealed class FakeModule(string id, bool enabledByDefault = true) : IIslandModule
    {
        public string Id => id;

        public string DisplayName => id;

        public string Description => id;

        public bool EnabledByDefault => enabledByDefault;

        public bool FailOnStart { get; init; }

        public int StartCount { get; private set; }

        public int StopCount { get; private set; }

        public string? UnavailableReason { get; init; }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            StartCount++;
            if (UnavailableReason is not null)
            {
                return Task.FromException(new ModuleUnavailableException(UnavailableReason));
            }

            return FailOnStart ? Task.FromException(new InvalidOperationException("falha simulada")) : Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            StopCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class InMemoryModuleStateStore : IModuleStateStore
    {
        public Dictionary<string, bool> Values { get; } = [];

        public Task<IReadOnlyDictionary<string, bool>> GetAllAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyDictionary<string, bool>>(new Dictionary<string, bool>(Values));

        public Task SetEnabledAsync(string moduleId, bool enabled, CancellationToken cancellationToken = default)
        {
            Values[moduleId] = enabled;
            return Task.CompletedTask;
        }
    }
}
