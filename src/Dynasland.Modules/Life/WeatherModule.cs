using System.Net.Http;
using System.Text.Json;
using Dynasland.Core.Activities;
using Dynasland.Core.Events;
using Dynasland.Core.Interfaces;
using Dynasland.Core.Models;
using Dynasland.Core.Modules;
using Dynasland.Core.Session;
using Microsoft.Extensions.Logging;
using Dynasland.Modules.Common;

namespace Dynasland.Modules.Life;

/// <summary>
/// Clima do Life Center: previsão do local escolhido nas configurações, atualizada a cada 30 minutos e
/// guardada em cache local. Sem conexão, mostra a última previsão marcada como desatualizada.
/// </summary>
public sealed class WeatherModule(
    IWeatherProvider provider,
    IWeatherCache cache,
    IIslandSettingsService settings,
    IEventBus bus,
    IUiDispatcher ui,
    TimeProvider time,
    IShortcutRegistry shortcuts,
    IGlanceRegistry glances,
    ILogger<WeatherModule> logger) : IIslandModule
{
    public const string ModuleId = "weather";
    public const string PanelActivityId = "weather.panel";
    public const string GlanceId = "weather.now";

    public static readonly TimeSpan RefreshInterval = TimeSpan.FromMinutes(30);
    public static readonly TimeSpan StaleAfter = TimeSpan.FromHours(3);

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly SemaphoreSlim _refreshGate = new(1, 1);
    private readonly List<IDisposable> _registrations = [];
    private WeatherContent? _content;
    private WeatherReport? _report;
    private string? _locationKey;
    private ITimer? _timer;

    public string Id => ModuleId;

    public string DisplayName => "Clima";

    public string Description => "Tempo atual e previsão dos próximos dias (Open-Meteo, gratuito). Escolha a cidade em Configurações › Geral.";

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _content = new WeatherContent(() => RefreshAsync(force: true));
        _registrations.Add(shortcuts.Register(new IslandShortcut("weather.panel", ModuleId, Glyphs.Weather, "Clima", OpenPanel)));
        _registrations.Add(bus.Subscribe<SessionChangedEvent>(e =>
        {
            if (e.Change == SessionChange.Resumed)
            {
                _ = RefreshAsync(force: false);
            }
        }));
        settings.Changed += OnSettingsChanged;

        _locationKey = settings.Current.WeatherLocation;
        await LoadCacheAsync(cancellationToken);
        _timer = time.CreateTimer(_ => _ = RefreshAsync(force: false), null, TimeSpan.FromSeconds(5), RefreshInterval);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        settings.Changed -= OnSettingsChanged;
        foreach (var registration in _registrations)
        {
            registration.Dispose();
        }

        _registrations.Clear();
        _timer?.Dispose();
        _timer = null;
        glances.Remove(GlanceId);
        _content = null;
        return Task.CompletedTask;
    }

    public void OpenPanel()
    {
        if (_content is { } content)
        {
            bus.PostActivity(new IslandActivity(PanelActivityId, ModuleId, ActivityKind.Transient, content) { OpenInteractive = true });
        }
    }

    /// <summary>Busca a previsão; sem <paramref name="force"/>, só se a última tiver mais de 30 minutos.</summary>
    public async Task RefreshAsync(bool force)
    {
        if (_content is not { } content || WeatherLocation.Parse(settings.Current.WeatherLocation) is not { } location)
        {
            ShowNoLocation();
            return;
        }

        if (!await _refreshGate.WaitAsync(0))
        {
            return;
        }

        try
        {
            if (!force && _report is { } recent && recent.Location.CacheKey == location.CacheKey && time.GetUtcNow() - recent.FetchedAt < RefreshInterval)
            {
                Present(recent);
                return;
            }

            if (_report is null)
            {
                ui.Post(() => content.ShowStatus(new StatusContent(StatusKind.Loading, "Buscando a previsão…", location.DisplayName)));
            }

            var report = await provider.GetForecastAsync(location);
            _report = report;
            await cache.SetAsync(location.CacheKey, JsonSerializer.Serialize(report, JsonOptions), report.FetchedAt);
            Present(report);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            logger.LogInformation("Previsão do tempo indisponível: {Message}", ex.Message);
            if (_report is { } previous)
            {
                Present(previous);
            }
            else
            {
                ui.Post(() => content.ShowStatus(new StatusContent(StatusKind.Error, "Sem conexão com o serviço de clima", "Tentaremos de novo em alguns minutos.")));
                glances.Remove(GlanceId);
            }
        }
        finally
        {
            _refreshGate.Release();
        }
    }

    private async Task LoadCacheAsync(CancellationToken cancellationToken)
    {
        if (WeatherLocation.Parse(settings.Current.WeatherLocation) is not { } location)
        {
            ShowNoLocation();
            return;
        }

        try
        {
            if (await cache.GetAsync(location.CacheKey, cancellationToken) is { } cached
                && JsonSerializer.Deserialize<WeatherReport>(cached.Payload, JsonOptions) is { } report)
            {
                _report = report with { FetchedAt = cached.FetchedAt };
                Present(_report);
            }
        }
        catch (JsonException)
        {
            // Cache de uma versão anterior: será substituído na próxima consulta.
        }
    }

    private void Present(WeatherReport report)
    {
        if (_content is not { } content)
        {
            return;
        }

        var now = time.GetUtcNow();
        ui.Post(() => content.Apply(report, now, StaleAfter));

        var current = report.Current;
        var stale = now - report.FetchedAt > StaleAfter ? " (desatualizado)" : string.Empty;
        glances.Set(new IslandGlance(
            GlanceId,
            Glyphs.Weather,
            $"{WeatherText.Temperature(current.TemperatureC)} {WeatherText.ConditionName(current.Condition)} · {report.Location.Name}{stale}",
            Order: 0,
            Invoke: OpenPanel,
            ShortText: $"{WeatherText.Temperature(current.TemperatureC)} {WeatherText.ConditionName(current.Condition)}"));
    }

    private void ShowNoLocation()
    {
        glances.Remove(GlanceId);
        if (_content is { } content)
        {
            ui.Post(() => content.ShowStatus(new StatusContent(StatusKind.Empty, "Escolha uma cidade", "Em Configurações › Geral › Clima.")));
        }
    }

    private void OnSettingsChanged(object? sender, IslandSettings value)
    {
        if (value.WeatherLocation == _locationKey)
        {
            return;
        }

        _locationKey = value.WeatherLocation;
        _report = null;
        _ = RefreshAsync(force: true);
    }
}
