using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dynasland.Core.Interfaces;
using Dynasland.Core.Modules;
using Dynasland.Modules.Water;
using Microsoft.Extensions.Logging;

namespace Dynasland.App.ViewModels.Hub;

/// <summary>Página da Central com os copos de água de hoje contra a meta.</summary>
public sealed partial class WaterPageViewModel : ObservableObject, IHubPage
{
    private readonly IWaterStore _store;
    private readonly IModuleOptions _options;
    private readonly IUiDispatcher _ui;
    private readonly TimeProvider _time;
    private readonly ILogger<WaterPageViewModel> _logger;

    public WaterPageViewModel(IWaterStore store, IModuleOptions options, IUiDispatcher ui, TimeProvider time, ILogger<WaterPageViewModel> logger)
    {
        _store = store;
        _options = options;
        _ui = ui;
        _time = time;
        _logger = logger;
        _store.Changed += OnChanged;
        _options.Changed += OnOptionChanged;
    }

    public HubPage Page => HubPage.Water;

    [ObservableProperty]
    private string _progressText = "0 de 8 copos";

    [ObservableProperty]
    private double _progress;

    [ObservableProperty]
    private string _message = string.Empty;

    /// <summary>Copos de hoje (a página reage à mudança para animar, venha de onde vier).</summary>
    [ObservableProperty]
    private int _glasses;

    [ObservableProperty]
    private int _goal = 8;

    /// <summary>
    /// Falso até a primeira carga aplicar os valores (vira verdadeiro logo depois de <see cref="Glasses"/>):
    /// a página usa isso para posicionar o nível sem animar ao abrir a aba.
    /// </summary>
    public bool HasLoaded { get; private set; }

    public async Task LoadAsync()
    {
        try
        {
            var glasses = await _store.GetAsync(Today());
            var goal = (int)_options.Get(WaterModule.ModuleId, WaterModule.Goal);
            _ui.Post(() =>
            {
                // Glasses por último: quando a página reage a ela, Progress e Goal já estão atualizados.
                Goal = goal;
                ProgressText = $"{glasses} de {goal} copos";
                Progress = Math.Min(1, glasses / (double)goal);
                Message = glasses >= goal
                    ? "Meta de hoje batida!"
                    : goal - glasses == 1 ? "Falta 1 copo para a meta." : $"Faltam {goal - glasses} copos para a meta.";
                Glasses = glasses;
                HasLoaded = true;
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao carregar o registro de água.");
        }
    }

    [RelayCommand]
    private async Task AddAsync()
    {
        try
        {
            await _store.AddAsync(Today(), 1);
            await LoadAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao registrar o copo de água.");
        }
    }

    [RelayCommand]
    private async Task RemoveAsync()
    {
        try
        {
            await _store.AddAsync(Today(), -1);
            await LoadAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao remover o copo de água.");
        }
    }

    public void Dispose()
    {
        _store.Changed -= OnChanged;
        _options.Changed -= OnOptionChanged;
    }

    private DateOnly Today() => DateOnly.FromDateTime(_time.GetLocalNow().DateTime);

    private void OnChanged(object? sender, EventArgs e) => _ = LoadAsync();

    private void OnOptionChanged(object? sender, (string ModuleId, string Key) change)
    {
        if (change.ModuleId == WaterModule.ModuleId)
        {
            _ = LoadAsync();
        }
    }
}
