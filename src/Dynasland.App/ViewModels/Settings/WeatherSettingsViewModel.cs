using System.Collections.ObjectModel;
using System.Net.Http;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dynasland.Core.Interfaces;
using Dynasland.Core.Models;

namespace Dynasland.App.ViewModels.Settings;

public sealed record WeatherLocationOption(WeatherLocation Location, IRelayCommand Select)
{
    public string Name => Location.DisplayName;
}

/// <summary>Escolha da cidade da previsão do tempo (busca pelo provedor de clima).</summary>
public sealed partial class WeatherSettingsViewModel : ObservableObject, IDisposable
{
    private readonly IWeatherProvider _provider;
    private readonly IIslandSettingsService _settings;
    private readonly IUiDispatcher _ui;
    private CancellationTokenSource? _search;

    public WeatherSettingsViewModel(IWeatherProvider provider, IIslandSettingsService settings, IUiDispatcher ui)
    {
        _provider = provider;
        _settings = settings;
        _ui = ui;
        ProviderName = provider.Name;
        Load(settings.Current);
        settings.Changed += OnSettingsChanged;
    }

    public string ProviderName { get; }

    public ObservableCollection<WeatherLocationOption> Results { get; } = [];

    [ObservableProperty]
    private string _query = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasLocation))]
    private string? _currentLocation;

    [ObservableProperty]
    private string? _searchMessage;

    public bool HasLocation => CurrentLocation is not null;

    public bool HasResults => Results.Count > 0;

    [RelayCommand]
    private async Task SearchAsync()
    {
        _search?.Cancel();
        var cts = _search = new CancellationTokenSource();
        SearchMessage = "Buscando…";
        try
        {
            var found = await _provider.SearchAsync(Query, cts.Token);
            Results.Clear();
            foreach (var location in found)
            {
                Results.Add(new WeatherLocationOption(location, new RelayCommand(() => Choose(location))));
            }

            SearchMessage = found.Count == 0 ? "Nenhuma cidade encontrada." : null;
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (HttpRequestException)
        {
            SearchMessage = "Sem conexão para buscar cidades agora.";
        }

        OnPropertyChanged(nameof(HasResults));
    }

    [RelayCommand]
    private Task ClearAsync() => _settings.UpdateAsync(s => s with { WeatherLocation = null });

    public void Dispose()
    {
        _settings.Changed -= OnSettingsChanged;
        _search?.Cancel();
    }

    private void Choose(WeatherLocation location)
    {
        _ = _settings.UpdateAsync(s => s with { WeatherLocation = location.Serialize() });
        Results.Clear();
        Query = string.Empty;
        SearchMessage = null;
        OnPropertyChanged(nameof(HasResults));
    }

    private void OnSettingsChanged(object? sender, IslandSettings settings) => _ui.Post(() => Load(settings));

    private void Load(IslandSettings settings) => CurrentLocation = WeatherLocation.Parse(settings.WeatherLocation)?.DisplayName;
}
