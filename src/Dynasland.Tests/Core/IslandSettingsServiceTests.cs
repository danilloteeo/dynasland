using Dynasland.Core.Interfaces;
using Dynasland.Core.Models;
using Dynasland.Core.Services;

namespace Dynasland.Tests.Core;

public class IslandSettingsServiceTests
{
    [Fact]
    public async Task Empty_store_yields_defaults()
    {
        using var service = new IslandSettingsService(new InMemorySettingsStore());

        await service.LoadAsync();

        Assert.Equal(IslandSettings.Default, service.Current);
    }

    [Fact]
    public async Task Invalid_values_fall_back_to_defaults_and_out_of_range_are_clamped()
    {
        var store = new InMemorySettingsStore(new Dictionary<string, string>
        {
            [IslandSettingsMapper.Keys.TopMargin] = "não é número",
            [IslandSettingsMapper.Keys.AnimationsEnabled] = "talvez",
            [IslandSettingsMapper.Keys.AnimationSpeed] = "99",
            [IslandSettingsMapper.Keys.ExpandedDurationSeconds] = "NaN",
        });
        using var service = new IslandSettingsService(store);

        await service.LoadAsync();

        Assert.Equal(IslandSettings.Default.TopMargin, service.Current.TopMargin);
        Assert.True(service.Current.AnimationsEnabled);
        Assert.Equal(3, service.Current.AnimationSpeed);
        Assert.Equal(IslandSettings.Default.ExpandedDurationSeconds, service.Current.ExpandedDurationSeconds);
    }

    [Fact]
    public async Task Update_persists_and_raises_changed()
    {
        var store = new InMemorySettingsStore();
        using var service = new IslandSettingsService(store);
        IslandSettings? raised = null;
        service.Changed += (_, s) => raised = s;

        await service.UpdateAsync(s => s with { MonitorDeviceName = @"\\.\DISPLAY2", TopMargin = 12.5 });

        Assert.NotNull(raised);
        Assert.Equal(@"\\.\DISPLAY2", raised.MonitorDeviceName);

        using var reloaded = new IslandSettingsService(store);
        await reloaded.LoadAsync();
        Assert.Equal(service.Current, reloaded.Current);
    }

    [Fact]
    public async Task Update_without_changes_does_not_write_or_notify()
    {
        var store = new InMemorySettingsStore();
        using var service = new IslandSettingsService(store);
        var raised = false;
        service.Changed += (_, _) => raised = true;

        await service.UpdateAsync(s => s with { });

        Assert.False(raised);
        Assert.Equal(0, store.WriteCount);
    }

    private sealed class InMemorySettingsStore(Dictionary<string, string>? initial = null) : ISettingsStore
    {
        private readonly Dictionary<string, string> _values = initial ?? [];

        public int WriteCount { get; private set; }

        public Task<IReadOnlyDictionary<string, string>> GetAllAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyDictionary<string, string>>(new Dictionary<string, string>(_values));

        public Task SetManyAsync(IReadOnlyDictionary<string, string> values, CancellationToken cancellationToken = default)
        {
            WriteCount++;
            foreach (var (key, value) in values)
            {
                _values[key] = value;
            }

            return Task.CompletedTask;
        }
    }
}
