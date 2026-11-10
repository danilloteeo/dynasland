using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dynasland.Core.Interfaces;
using Dynasland.Core.Models;
using Dynasland.Core.Modules;
using Microsoft.Extensions.Logging;

namespace Dynasland.App.ViewModels.Settings;

/// <summary>Categoria Comportamento: como a ilha reage a cliques, tela cheia e módulo fixado.</summary>
public sealed partial class BehaviorSettingsViewModel : ObservableObject, IDisposable
{
    private readonly IIslandSettingsService _settings;
    private readonly ModuleManager _modules;
    private readonly IUiDispatcher _ui;
    private readonly ILogger<BehaviorSettingsViewModel> _logger;
    private bool _loading;

    public BehaviorSettingsViewModel(IIslandSettingsService settings, ModuleManager modules, IUiDispatcher ui, ILogger<BehaviorSettingsViewModel> logger)
    {
        _settings = settings;
        _modules = modules;
        _ui = ui;
        _logger = logger;
        Load(settings.Current);
        settings.Changed += OnSettingsChanged;
    }

    [ObservableProperty]
    private bool _collapseOnClickOutside;

    [ObservableProperty]
    private DoubleClickAction _doubleClickAction;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPinnedModule))]
    private string? _pinnedModuleName;

    public bool HasPinnedModule => PinnedModuleName is not null;

    partial void OnCollapseOnClickOutsideChanged(bool value) => Save(s => s with { CollapseOnClickOutside = value });

    partial void OnDoubleClickActionChanged(DoubleClickAction value) => Save(s => s with { DoubleClickAction = value });

    [RelayCommand]
    private void Unpin() => Save(s => s with { PinnedModuleId = null });

    public void Dispose() => _settings.Changed -= OnSettingsChanged;

    private void OnSettingsChanged(object? sender, IslandSettings settings) => _ui.Post(() => Load(settings));

    private void Load(IslandSettings s)
    {
        _loading = true;
        try
        {
            CollapseOnClickOutside = s.CollapseOnClickOutside;
            DoubleClickAction = s.DoubleClickAction;
            PinnedModuleName = s.PinnedModuleId is { } id
                ? _modules.GetModules().FirstOrDefault(m => m.Id == id)?.DisplayName ?? id
                : null;
        }
        finally
        {
            _loading = false;
        }
    }

    private async void Save(Func<IslandSettings, IslandSettings> change)
    {
        if (_loading)
        {
            return;
        }

        try
        {
            await _settings.UpdateAsync(change);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao salvar comportamento.");
        }
    }
}
