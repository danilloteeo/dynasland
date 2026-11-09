using System.Collections.ObjectModel;
using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dynasland.Core.Interfaces;
using Dynasland.Core.Models;
using Dynasland.Core.Modules;
using Dynasland.Infrastructure.Database;
using Dynasland.Modules.Notifications;
using Microsoft.Extensions.Logging;

namespace Dynasland.App.ViewModels;

public enum SettingsSection
{
    Modules,
    Island,
    Notifications,
    About,
}

public enum ModuleStatus
{
    Active,
    Disabled,
    Unavailable,
    Failed,
    Applying,
}

/// <summary>Janela de configurações: tudo o que é exibido vem dos serviços reais e é aplicado na hora.</summary>
public sealed partial class SettingsViewModel : ObservableObject, IDisposable
{
    private readonly ModuleManager _modules;
    private readonly IIslandSettingsService _settings;
    private readonly IMonitorService _monitors;
    private readonly NotificationPreferences _notificationPreferences;
    private readonly IUiDispatcher _ui;
    private readonly ILogger<SettingsViewModel> _logger;
    private bool _loading;

    public SettingsViewModel(
        ModuleManager modules,
        IIslandSettingsService settings,
        IMonitorService monitors,
        NotificationPreferences notificationPreferences,
        DatabaseOptions database,
        IUiDispatcher ui,
        ILogger<SettingsViewModel> logger)
    {
        _modules = modules;
        _settings = settings;
        _monitors = monitors;
        _notificationPreferences = notificationPreferences;
        _ui = ui;
        _logger = logger;

        DataPath = database.DatabasePath;
        Version = Assembly.GetEntryAssembly()?.GetName().Version?.ToString(3) ?? "—";

        modules.ModulesChanged += OnModulesChanged;
        settings.Changed += OnSettingsChanged;
        Refresh();
    }

    public ObservableCollection<ModuleSettingViewModel> Modules { get; } = [];

    public ObservableCollection<MonitorOptionViewModel> MonitorOptions { get; } = [];

    public ObservableCollection<BlockedAppViewModel> BlockedApps { get; } = [];

    public string DataPath { get; }

    public string Version { get; }

    [ObservableProperty]
    private SettingsSection _section = SettingsSection.Modules;

    [ObservableProperty]
    private string _modulesSummary = string.Empty;

    [ObservableProperty]
    private bool _isIslandVisible;

    [ObservableProperty]
    private bool _animationsEnabled;

    [ObservableProperty]
    private double _animationSpeed;

    [ObservableProperty]
    private double _expandedDurationSeconds;

    [ObservableProperty]
    private double _topMargin;

    public bool HasBlockedApps => BlockedApps.Count > 0;

    [RelayCommand]
    private void Navigate(SettingsSection section) => Section = section;

    /// <summary>Recarrega tudo dos serviços (ao abrir a janela e quando algo muda fora dela).</summary>
    public void Refresh()
    {
        _loading = true;
        try
        {
            RefreshModules();

            var current = _settings.Current;
            IsIslandVisible = current.IsVisible;
            AnimationsEnabled = current.AnimationsEnabled;
            AnimationSpeed = current.AnimationSpeed;
            ExpandedDurationSeconds = current.ExpandedDurationSeconds;
            TopMargin = current.TopMargin;

            RefreshMonitors(current);
            RefreshBlockedApps();
        }
        finally
        {
            _loading = false;
        }
    }

    private void RefreshModules()
    {
        var infos = _modules.GetModules();
        Modules.Clear();
        foreach (var info in infos)
        {
            Modules.Add(new ModuleSettingViewModel(info, SetModuleEnabledAsync));
        }

        var active = infos.Count(m => m.IsRunning);
        var unavailable = infos.Count(m => m.IsUnavailable);
        ModulesSummary = $"{active} de {infos.Count} ativos" + (unavailable > 0 ? $" · {unavailable} indisponíveis neste computador" : string.Empty);
    }

    private void RefreshMonitors(IslandSettings current)
    {
        var monitors = _monitors.GetMonitors();
        var selectedConnected = monitors.Any(m => string.Equals(m.DeviceName, current.MonitorDeviceName, StringComparison.OrdinalIgnoreCase));

        MonitorOptions.Clear();
        MonitorOptions.Add(new MonitorOptionViewModel(null, "Monitor principal", "Acompanha o monitor principal do Windows",
            current.MonitorDeviceName is null || !selectedConnected, SelectMonitor));
        foreach (var monitor in monitors)
        {
            var name = monitor.DisplayNumber is { } number ? $"Monitor {number}" : monitor.DeviceName;
            var detail = $"{monitor.Bounds.Width} × {monitor.Bounds.Height} · escala {Math.Round(monitor.Scale * 100)}%" + (monitor.IsPrimary ? " · principal" : string.Empty);
            MonitorOptions.Add(new MonitorOptionViewModel(monitor.DeviceName, name, detail,
                string.Equals(monitor.DeviceName, current.MonitorDeviceName, StringComparison.OrdinalIgnoreCase), SelectMonitor));
        }
    }

    private void RefreshBlockedApps()
    {
        BlockedApps.Clear();
        foreach (var appId in _notificationPreferences.BlockedApps.Order(StringComparer.OrdinalIgnoreCase))
        {
            BlockedApps.Add(new BlockedAppViewModel(appId, UnblockAsync));
        }

        OnPropertyChanged(nameof(HasBlockedApps));
    }

    private async Task SetModuleEnabledAsync(string moduleId, bool enabled)
    {
        try
        {
            await _modules.SetEnabledAsync(moduleId, enabled);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao alterar o módulo {Module}.", moduleId);
        }

        _ui.Post(RefreshModules);
    }

    private async Task UnblockAsync(string appId)
    {
        await _notificationPreferences.SetBlockedAsync(appId, blocked: false);
        _ui.Post(RefreshBlockedApps);
    }

    private void SelectMonitor(string? deviceName) => Save(s => s with { MonitorDeviceName = deviceName });

    partial void OnIsIslandVisibleChanged(bool value) => Save(s => s with { IsVisible = value });

    partial void OnAnimationsEnabledChanged(bool value) => Save(s => s with { AnimationsEnabled = value });

    partial void OnAnimationSpeedChanged(double value) => Save(s => s with { AnimationSpeed = Math.Round(value, 2) });

    partial void OnExpandedDurationSecondsChanged(double value) => Save(s => s with { ExpandedDurationSeconds = Math.Round(value) });

    partial void OnTopMarginChanged(double value) => Save(s => s with { TopMargin = Math.Round(value) });

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
            _logger.LogError(ex, "Falha ao salvar configuração.");
        }
    }

    private void OnModulesChanged(object? sender, EventArgs e) => _ui.Post(RefreshModules);

    private void OnSettingsChanged(object? sender, IslandSettings settings) => _ui.Post(() =>
    {
        // Mudanças vindas de outro lugar (ex.: menu da bandeja) aparecem aqui sem regravar.
        _loading = true;
        try
        {
            IsIslandVisible = settings.IsVisible;
            RefreshMonitors(settings);
        }
        finally
        {
            _loading = false;
        }
    });

    public void Dispose()
    {
        _modules.ModulesChanged -= OnModulesChanged;
        _settings.Changed -= OnSettingsChanged;
    }
}

public sealed partial class ModuleSettingViewModel : ObservableObject
{
    private readonly Func<string, bool, Task> _setEnabled;

    public ModuleSettingViewModel(ModuleInfo info, Func<string, bool, Task> setEnabled)
    {
        _setEnabled = setEnabled;
        Id = info.Id;
        Name = info.DisplayName;
        Description = info.Description;
        _isEnabled = info.IsEnabled;
        CanToggle = !info.IsUnavailable;
        Status = info switch
        {
            { IsUnavailable: true } => ModuleStatus.Unavailable,
            { IsRunning: true } => ModuleStatus.Active,
            { IsEnabled: false } => ModuleStatus.Disabled,
            _ => ModuleStatus.Failed,
        };
        Reason = info.Error;
    }

    public string Id { get; }

    public string Name { get; }

    public string Description { get; }

    public bool CanToggle { get; }

    /// <summary>Motivo de indisponibilidade ou falha.</summary>
    public string? Reason { get; }

    public bool HasReason => !string.IsNullOrWhiteSpace(Reason);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    private ModuleStatus _status;

    [ObservableProperty]
    private bool _isEnabled;

    public string StatusText => Status switch
    {
        ModuleStatus.Active => "Ativo",
        ModuleStatus.Disabled => "Desativado",
        ModuleStatus.Unavailable => "Indisponível",
        ModuleStatus.Failed => "Falhou ao iniciar",
        _ => "Aplicando…",
    };

    partial void OnIsEnabledChanged(bool value)
    {
        Status = ModuleStatus.Applying;
        _ = _setEnabled(Id, value);
    }
}

public sealed partial class MonitorOptionViewModel(string? deviceName, string name, string detail, bool isSelected, Action<string?> select) : ObservableObject
{
    public string Name { get; } = name;

    public string Detail { get; } = detail;

    [ObservableProperty]
    private bool _isSelected = isSelected;

    partial void OnIsSelectedChanged(bool value)
    {
        if (value)
        {
            select(deviceName);
        }
    }
}

public sealed partial class BlockedAppViewModel(string appId, Func<string, Task> unblock)
{
    public string AppId { get; } = appId;

    [RelayCommand]
    private Task Unblock() => unblock(AppId);
}
