using System.Text.Json;
using Dynasland.Core.Activities;
using Dynasland.Core.Interfaces;
using Dynasland.Core.Models;
using Dynasland.Core.Modules;
using Dynasland.Core.Shortcuts;
using Dynasland.Infrastructure.Repositories;
using Dynasland.Infrastructure.Weather;
using Dynasland.Modules.Life;
using Dynasland.Tests.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace Dynasland.Tests.Modules;

public class LifeCenterTests : IDisposable
{
    private static readonly DateTimeOffset T0 = new(2026, 11, 10, 12, 0, 0, TimeSpan.Zero);
    private readonly ActivityRecorder _recorder = new();

    public void Dispose() => _recorder.Dispose();

    // Resposta real do Open-Meteo (reduzida a 2 dias).
    private const string ForecastJson = """
        {"latitude":-23.5,"longitude":-46.625,"utc_offset_seconds":-10800,"timezone":"America/Sao_Paulo",
         "current":{"time":"2026-11-10T09:00","interval":900,"temperature_2m":24.3,"apparent_temperature":25.1,"relative_humidity_2m":68,
                    "weather_code":3,"wind_speed_10m":11.2,"is_day":1},
         "daily":{"time":["2026-11-10","2026-11-11"],"weather_code":[61,0],"temperature_2m_max":[27.4,30.1],"temperature_2m_min":[18.2,19.0],
                  "precipitation_probability_max":[80,null]}}
        """;

    [Fact]
    public void Open_meteo_response_is_mapped()
    {
        var location = new WeatherLocation("São Paulo", "São Paulo", "Brasil", -23.55, -46.63);
        var response = JsonSerializer.Deserialize<OpenMeteoWeatherProvider.ForecastResponse>(ForecastJson)!;

        var report = OpenMeteoWeatherProvider.Map(location, response, T0);

        Assert.Equal(24.3, report.Current.TemperatureC);
        Assert.Equal(WeatherCondition.Cloudy, report.Current.Condition);
        Assert.Equal(68, report.Current.HumidityPercent);
        Assert.Equal(new DateTimeOffset(2026, 11, 10, 9, 0, 0, TimeSpan.FromHours(-3)), report.Current.ObservedAt);
        Assert.Equal(2, report.Days.Count);
        Assert.Equal(WeatherCondition.Rain, report.Days[0].Condition);
        Assert.Equal(80, report.Days[0].PrecipitationChancePercent);
        Assert.Null(report.Days[1].PrecipitationChancePercent);
        Assert.Equal(T0, report.FetchedAt);
    }

    [Theory]
    [InlineData(0, WeatherCondition.Clear)]
    [InlineData(2, WeatherCondition.PartlyCloudy)]
    [InlineData(45, WeatherCondition.Fog)]
    [InlineData(53, WeatherCondition.Drizzle)]
    [InlineData(81, WeatherCondition.Rain)]
    [InlineData(73, WeatherCondition.Snow)]
    [InlineData(96, WeatherCondition.Thunderstorm)]
    public void Wmo_codes_map_to_conditions(int code, WeatherCondition expected) =>
        Assert.Equal(expected, OpenMeteoWeatherProvider.ConditionOf(code));

    [Fact]
    public void Location_round_trips_through_settings()
    {
        var location = new WeatherLocation("Rio de Janeiro", "Rio de Janeiro", "Brasil", -22.9068, -43.1729);

        Assert.Equal(location, WeatherLocation.Parse(location.Serialize()));
        Assert.Equal("Rio de Janeiro, Brasil", location.DisplayName);
        Assert.Null(WeatherLocation.Parse("lixo"));
    }

    [Fact]
    public void Stale_weather_is_marked_and_never_shown_as_current()
    {
        var location = new WeatherLocation("Curitiba", "", "Brasil", -25.4, -49.3);
        var report = new WeatherReport(location, new CurrentWeather(15, 14, 80, 5, WeatherCondition.Fog, true, T0), [], T0);
        var content = new WeatherContent(() => Task.CompletedTask);

        content.Apply(report, T0.AddMinutes(20), WeatherModule.StaleAfter);
        Assert.False(content.IsStale);
        Assert.StartsWith("Atualizado", content.UpdatedText);

        content.Apply(report, T0.AddHours(5), WeatherModule.StaleAfter);
        Assert.True(content.IsStale);
        Assert.StartsWith("Desatualizado", content.UpdatedText);
    }

    [Fact]
    public async Task Weather_module_fetches_caches_and_falls_back_to_cache_offline()
    {
        using var db = new TemporaryDatabase();
        await db.CreateRunner().MigrateAsync();
        var time = new FakeTimeProvider(T0);
        var settings = new FakeIslandSettings();
        var location = new WeatherLocation("Recife", "", "Brasil", -8.05, -34.9);
        await settings.UpdateAsync(s => s with { WeatherLocation = location.Serialize() });
        var provider = new FakeWeatherProvider(time);
        var glances = new GlanceRegistry();
        var module = new WeatherModule(provider, new SqliteWeatherCache(db.Factory), settings, _recorder.Bus, new InlineDispatcher(), time,
            new ShortcutRegistry(), glances, NullLogger<WeatherModule>.Instance);

        await module.StartAsync(CancellationToken.None);
        await module.RefreshAsync(force: true);
        Assert.Contains("29°", Assert.Single(glances.Glances).Text);

        // Novo início, sem conexão: usa o cache e marca como desatualizado depois de 3 h.
        provider.Offline = true;
        time.Advance(TimeSpan.FromHours(4));
        var glances2 = new GlanceRegistry();
        var restarted = new WeatherModule(provider, new SqliteWeatherCache(db.Factory), settings, _recorder.Bus, new InlineDispatcher(), time,
            new ShortcutRegistry(), glances2, NullLogger<WeatherModule>.Instance);
        await restarted.StartAsync(CancellationToken.None);
        await restarted.RefreshAsync(force: true);

        Assert.Contains("desatualizado", Assert.Single(glances2.Glances).Text);
        await module.StopAsync(CancellationToken.None);
        await restarted.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Calendar_reminder_fires_on_time_and_after_missed_while_closed()
    {
        using var db = new TemporaryDatabase();
        await db.CreateRunner().MigrateAsync();
        var store = new SqliteCalendarStore(db.Factory);
        var time = new FakeTimeProvider(T0);
        var glances = new GlanceRegistry();

        await store.SaveAsync(new CalendarEvent(0, "Reunião", "", T0.AddMinutes(30), T0.AddMinutes(90), false, 15, null));
        await store.SaveAsync(new CalendarEvent(0, "Já passou", "", T0.AddHours(-3), T0.AddHours(-2), false, 10, null));

        var module = new CalendarModule(store, new NoHub(), _recorder.Bus, time, new ShortcutRegistry(), glances, new SilentSound(),
            TestOptions.Create(), new GlobalShortcutRegistry(new InMemorySettingsStore()), NullLogger<CalendarModule>.Instance);
        await module.StartAsync(CancellationToken.None);
        await module.RescheduleAsync();

        Assert.Empty(_recorder.Posted);
        Assert.DoesNotContain(await store.PendingRemindersAsync(), e => e.Title == "Já passou");
        Assert.Contains("Reunião · em 30 min", Assert.Single(glances.Glances).Text);

        time.Advance(TimeSpan.FromMinutes(15));
        await WaitUntil(() => _recorder.Posted.Count == 1);
        var reminder = Assert.IsType<StatusContent>(_recorder.Posted[0].Content);
        Assert.Equal("Reunião", reminder.Title);
        Assert.Equal("em 15 min", reminder.Message);
        Assert.Equal(ActivityPriority.High, _recorder.Posted[0].Priority);

        await module.StopAsync(CancellationToken.None);
    }

    [Theory]
    [InlineData("9:30", 9, 30)]
    [InlineData("14:05", 14, 5)]
    [InlineData("8", 8, 0)]
    public void Calendar_times_are_parsed(string text, int hours, int minutes)
    {
        Assert.True(Dynasland.App.ViewModels.Hub.CalendarPageViewModel.TryParseTime(text, out var time));
        Assert.Equal(new TimeSpan(hours, minutes, 0), time);
    }

    private static async Task WaitUntil(Func<bool> condition)
    {
        for (var i = 0; i < 200 && !condition(); i++)
        {
            await Task.Delay(10);
        }

        Assert.True(condition());
    }

    private sealed class NoHub : IHubService
    {
        public void Open(HubPage page)
        {
        }
    }

    private sealed class FakeWeatherProvider(TimeProvider time) : IWeatherProvider
    {
        public bool Offline { get; set; }

        public string Name => "Teste";

        public Task<IReadOnlyList<WeatherLocation>> SearchAsync(string query, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<WeatherLocation>>([]);

        public Task<WeatherReport> GetForecastAsync(WeatherLocation location, CancellationToken cancellationToken = default) => Offline
            ? throw new HttpRequestException("sem rede")
            : Task.FromResult(new WeatherReport(location, new CurrentWeather(29, 31, 70, 12, WeatherCondition.Clear, true, time.GetUtcNow()), [], time.GetUtcNow()));
    }
}
