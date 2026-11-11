using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dynasland.App.Services;
using Dynasland.Core.Interfaces;
using Dynasland.Core.Models;
using Dynasland.Core.Modules;
using Dynasland.Infrastructure.Database;
using Dynasland.Infrastructure.Logging;
using Dynasland.Infrastructure.Updates;
using Dynasland.Modules.Notifications;
using Microsoft.Extensions.Logging;

namespace Dynasland.App.ViewModels;

public enum SettingsSection
{
    General,
    Appearance,
    Animations,
    Behavior,
    Monitors,
    Modules,
    Notifications,
    Privacy,
    Shortcuts,
    Storage,
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
    public static readonly IReadOnlyList<(string Name, string Hex)> AccentPresets =
    [
        ("Verde", "#30D158"),
        ("Azul", "#0A84FF"),
        ("Ciano", "#64D2FF"),
        ("Roxo", "#BF5AF2"),
        ("Rosa", "#FF375F"),
        ("Laranja", "#FF9F0A"),
        ("Amarelo", "#FFD60A"),
        ("Grafite", "#8E8E93"),
    ];

    private readonly ModuleManager _modules;
    private readonly IModuleOptions _moduleOptions;
    private readonly IIslandSettingsService _settings;
    private readonly IMonitorService _monitors;
    private readonly NotificationPreferences _notifications;
    private readonly IStartupRegistration _startup;
    private readonly IUpdateChecker _updates;
    private readonly IShellService _shell;
    private readonly IUiDispatcher _ui;
    private readonly ILogger<SettingsViewModel> _logger;
    private bool _loading;

    public SettingsViewModel(
        ModuleManager modules,
        IModuleOptions moduleOptions,
        IIslandSettingsService settings,
        IMonitorService monitors,
        NotificationPreferences notifications,
        IStartupRegistration startup,
        IUpdateChecker updates,
        UpdateFlow updateFlow,
        IShellService shell,
        DatabaseOptions database,
        IUiDispatcher ui,
        ILogger<SettingsViewModel> logger,
        Settings.BehaviorSettingsViewModel behavior,
        Settings.PrivacySettingsViewModel privacy,
        Settings.ShortcutsSettingsViewModel shortcuts,
        Settings.StorageSettingsViewModel storage,
        Settings.WeatherSettingsViewModel weather,
        Settings.SpotifySettingsViewModel spotify)
    {
        Weather = weather;
        Updates = updateFlow;
        Spotify = spotify;
        Behavior = behavior;
        Privacy = privacy;
        Shortcuts = shortcuts;
        Storage = storage;
        _modules = modules;
        _moduleOptions = moduleOptions;
        _settings = settings;
        _monitors = monitors;
        _notifications = notifications;
        _startup = startup;
        _updates = updates;
        _shell = shell;
        _ui = ui;
        _logger = logger;

        DataPath = database.DatabasePath;
        LogPath = FileLoggerProvider.DefaultPath;
        Version = GitHubUpdateChecker.CurrentVersion.ToString(3);
        UpdateSource = updates.SourceDescription ?? string.Empty;
        UpdateStatusText = updates.IsConfigured ? "Ainda não verificado nesta sessão." : "Nenhuma fonte de atualizações configurada nesta versão.";

        modules.ModulesChanged += OnModulesChanged;
        settings.Changed += OnSettingsChanged;
        notifications.Changed += OnNotificationsChanged;
        Refresh();
    }

    /// <summary>Categorias com view model próprio.</summary>
    public Settings.BehaviorSettingsViewModel Behavior { get; }

    public Settings.PrivacySettingsViewModel Privacy { get; }

    public Settings.ShortcutsSettingsViewModel Shortcuts { get; }

    public Settings.StorageSettingsViewModel Storage { get; }

    public Settings.WeatherSettingsViewModel Weather { get; }

    public Settings.SpotifySettingsViewModel Spotify { get; }

    public ObservableCollection<ModuleSettingViewModel> Modules { get; } = [];

    public ObservableCollection<MonitorOptionViewModel> MonitorOptions { get; } = [];

    public ObservableCollection<AccentOptionViewModel> AccentOptions { get; } = [];

    public ObservableCollection<IslandThemeOptionViewModel> IslandThemeOptions { get; } = [];

    public ObservableCollection<NotificationAppViewModel> NotificationApps { get; } = [];

    public string DataPath { get; }

    public string LogPath { get; }

    public string Version { get; }

    public string UpdateSource { get; }

    public bool UpdatesConfigured => _updates.IsConfigured;

    /// <summary>Atualização pelo aplicativo (mesmo fluxo mostrado na ilha).</summary>
    public UpdateFlow Updates { get; }

    public string Language => "Português (Brasil)";

    [ObservableProperty]
    private SettingsSection _section = SettingsSection.General;

    [ObservableProperty]
    private string _modulesSummary = string.Empty;

    // Geral
    [ObservableProperty]
    private bool _startWithWindows;

    [ObservableProperty]
    private bool _startHidden;

    [ObservableProperty]
    private bool _isIslandVisible;

    [ObservableProperty]
    private bool _hideInFullscreen;

    [ObservableProperty]
    private double _topMargin;

    // Aparência
    [ObservableProperty]
    private AppTheme _theme;

    [ObservableProperty]
    private bool _islandThemeFollowsMedia;

    [ObservableProperty]
    private IdleDisplay _idleDisplay;

    [ObservableProperty]
    private double _islandOpacity;

    [ObservableProperty]
    private double _shadowIntensity;

    [ObservableProperty]
    private double _roundness;

    [ObservableProperty]
    private double _compactWidth;

    [ObservableProperty]
    private double _compactHeight;

    [ObservableProperty]
    private double _maxWidth;

    // Animações
    [ObservableProperty]
    private bool _animationsEnabled;

    [ObservableProperty]
    private double _animationSpeed;

    [ObservableProperty]
    private double _animationIntensity;

    [ObservableProperty]
    private double _expandedDurationSeconds;

    // Notificações
    [ObservableProperty]
    private NotificationFilterMode _notificationMode;

    [ObservableProperty]
    private double _notificationDuration;

    [ObservableProperty]
    private bool _notificationsCanInterrupt;

    // Sistema
    [ObservableProperty]
    private bool _checkForUpdates;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CheckUpdatesNowCommand))]
    private bool _isCheckingUpdates;

    [ObservableProperty]
    private string _updateStatusText;



    public bool HasNotificationApps => NotificationApps.Count > 0;

    [RelayCommand]
    private void Navigate(SettingsSection section) => Section = section;

    /// <summary>Recarrega tudo dos serviços (ao abrir a janela e quando algo muda fora dela).</summary>
    public void Refresh()
    {
        _loading = true;
        try
        {
            RefreshModules();
            LoadSettings(_settings.Current);
            StartWithWindows = _startup.IsEnabled;
            RefreshNotifications();
        }
        finally
        {
            _loading = false;
        }
    }

    private void LoadSettings(IslandSettings s)
    {
        StartHidden = s.StartHidden;
        IsIslandVisible = s.IsVisible;
        HideInFullscreen = s.HideInFullscreen;
        TopMargin = s.TopMargin;
        Theme = s.Theme;
        IslandThemeFollowsMedia = s.IslandThemeFollowsMedia;
        IdleDisplay = s.IdleDisplay;
        IslandOpacity = s.IslandOpacity;
        ShadowIntensity = s.ShadowIntensity;
        Roundness = s.Roundness;
        CompactWidth = s.CompactWidth;
        CompactHeight = s.CompactHeight;
        MaxWidth = s.MaxWidth;
        AnimationsEnabled = s.AnimationsEnabled;
        AnimationSpeed = s.AnimationSpeed;
        AnimationIntensity = s.AnimationIntensity;
        ExpandedDurationSeconds = s.ExpandedDurationSeconds;
        CheckForUpdates = s.CheckForUpdates;
        RefreshMonitors(s);
        RefreshAccents(s);
        RefreshIslandThemes(s);
    }

    private void RefreshModules()
    {
        var infos = _modules.GetModules();
        Modules.Clear();
        foreach (var info in infos)
        {
            var options = (info.Options ?? [])
                .Select(option => new ModuleOptionViewModel(option, _moduleOptions.Get(info.Id, option), value => SetModuleOptionAsync(info.Id, option, value)))
                .ToList();
            Modules.Add(new ModuleSettingViewModel(info, options, SetModuleEnabledAsync));
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

    private void RefreshAccents(IslandSettings current)
    {
        AccentOptions.Clear();
        foreach (var (name, hex) in AccentPresets)
        {
            AccentOptions.Add(new AccentOptionViewModel(name, hex, string.Equals(hex, current.AccentColor, StringComparison.OrdinalIgnoreCase), SelectAccent));
        }
    }

    private void RefreshIslandThemes(IslandSettings current)
    {
        var accent = (Color)ColorConverter.ConvertFromString(current.AccentColor);
        IslandThemeOptions.Clear();
        foreach (var (theme, name) in IslandThemes.All)
        {
            IslandThemeOptions.Add(new IslandThemeOptionViewModel(theme, name, IslandThemes.Background(theme, accent, 1),
                theme == current.IslandTheme, SelectIslandTheme));
        }
    }

    private void RefreshNotifications()
    {
        var wasLoading = _loading;
        _loading = true;
        try
        {
            NotificationMode = _notifications.Mode;
            NotificationDuration = _notifications.DurationSeconds;
            NotificationsCanInterrupt = _notifications.CanInterrupt;
        }
        finally
        {
            _loading = wasLoading;
        }

        NotificationApps.Clear();
        foreach (var app in _notifications.KnownApps)
        {
            var shows = app.Rule ?? _notifications.Mode == NotificationFilterMode.AllExceptBlocked;
            NotificationApps.Add(new NotificationAppViewModel(app, shows, _notifications, _logger));
        }

        OnPropertyChanged(nameof(HasNotificationApps));
    }

    // Ações -------------------------------------------------------------

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

    private Task SetModuleOptionAsync(string moduleId, ModuleOption option, double value) =>
        Guard(() => _moduleOptions.SetAsync(moduleId, option, value), "opção do módulo");


    private void SelectMonitor(string? deviceName) => Save(s => s with { MonitorDeviceName = deviceName });

    private void SelectAccent(string hex)
    {
        Save(s => s with { AccentColor = hex });
        // A amostra do tema "Cor de destaque" acompanha a cor escolhida.
        RefreshIslandThemes(_settings.Current with { AccentColor = hex });
    }

    private void SelectIslandTheme(IslandTheme theme) => Save(s => s with { IslandTheme = theme });

    partial void OnStartWithWindowsChanged(bool value)
    {
        if (!_loading)
        {
            _startup.SetEnabled(value);
        }
    }

    partial void OnStartHiddenChanged(bool value) => Save(s => s with { StartHidden = value });

    partial void OnIsIslandVisibleChanged(bool value) => Save(s => s with { IsVisible = value });

    partial void OnHideInFullscreenChanged(bool value) => Save(s => s with { HideInFullscreen = value });

    partial void OnTopMarginChanged(double value) => Save(s => s with { TopMargin = Math.Round(value) });

    partial void OnThemeChanged(AppTheme value) => Save(s => s with { Theme = value });

    partial void OnIslandThemeFollowsMediaChanged(bool value) => Save(s => s with { IslandThemeFollowsMedia = value });

    partial void OnIdleDisplayChanged(IdleDisplay value) => Save(s => s with { IdleDisplay = value });

    partial void OnIslandOpacityChanged(double value) => Save(s => s with { IslandOpacity = Math.Round(value, 2) });

    partial void OnShadowIntensityChanged(double value) => Save(s => s with { ShadowIntensity = Math.Round(value, 2) });

    partial void OnRoundnessChanged(double value) => Save(s => s with { Roundness = Math.Round(value, 2) });

    partial void OnCompactWidthChanged(double value) => Save(s => s with { CompactWidth = Math.Round(value) });

    partial void OnCompactHeightChanged(double value) => Save(s => s with { CompactHeight = Math.Round(value) });

    partial void OnMaxWidthChanged(double value) => Save(s => s with { MaxWidth = Math.Round(value) });

    partial void OnAnimationsEnabledChanged(bool value) => Save(s => s with { AnimationsEnabled = value });

    partial void OnAnimationSpeedChanged(double value) => Save(s => s with { AnimationSpeed = Math.Round(value, 2) });

    partial void OnAnimationIntensityChanged(double value) => Save(s => s with { AnimationIntensity = Math.Round(value, 2) });

    partial void OnExpandedDurationSecondsChanged(double value) => Save(s => s with { ExpandedDurationSeconds = Math.Round(value) });

    partial void OnCheckForUpdatesChanged(bool value) => Save(s => s with { CheckForUpdates = value });

    partial void OnNotificationModeChanged(NotificationFilterMode value)
    {
        if (!_loading)
        {
            _ = Guard(() => _notifications.SetModeAsync(value), "modo de notificações");
        }
    }

    partial void OnNotificationDurationChanged(double value)
    {
        if (!_loading)
        {
            _ = Guard(() => _notifications.SetDurationAsync(value), "duração das notificações");
        }
    }

    partial void OnNotificationsCanInterruptChanged(bool value)
    {
        if (!_loading)
        {
            _ = Guard(() => _notifications.SetCanInterruptAsync(value), "prioridade das notificações");
        }
    }

    [RelayCommand(CanExecute = nameof(CanCheckUpdates))]
    private async Task CheckUpdatesNowAsync()
    {
        IsCheckingUpdates = true;
        UpdateStatusText = "Verificando…";
        try
        {
            var result = await _updates.CheckAsync();
            Updates.Offer(result);
            UpdateStatusText = Describe(result);
        }
        finally
        {
            IsCheckingUpdates = false;
        }
    }

    private bool CanCheckUpdates() => _updates.IsConfigured && !IsCheckingUpdates;

    public static string Describe(UpdateCheckResult result) => result.Status switch
    {
        UpdateStatus.UpToDate => $"Você está usando a versão mais recente ({result.LatestVersion}).",
        UpdateStatus.UpdateAvailable => $"Nova versão disponível: {result.LatestVersion}.",
        UpdateStatus.NoReleases => "Nenhuma versão publicada ainda.",
        UpdateStatus.NotConfigured => "Nenhuma fonte de atualizações configurada nesta versão.",
        _ => result.Message ?? "Não foi possível verificar agora.",
    };

    [RelayCommand]
    private void OpenLogsFolder()
    {
        var folder = Path.GetDirectoryName(LogPath)!;
        Directory.CreateDirectory(folder);
        _shell.Open(folder);
    }

    /// <summary>Volta tudo ao padrão (a inicialização com o Windows não é alterada). Confirmado pela janela.</summary>
    public async Task RestoreDefaultsAsync()
    {
        try
        {
            await _settings.ResetAsync();
            await _moduleOptions.LoadAsync();
            await _notifications.LoadAsync();
            await _modules.ResetAsync();
            await Shortcuts.ReloadAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao restaurar as configurações.");
        }

        _ui.Post(Refresh);
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
            _logger.LogError(ex, "Falha ao salvar configuração.");
        }
    }

    private async Task Guard(Func<Task> action, string what)
    {
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao salvar {What}.", what);
        }
    }

    private void OnModulesChanged(object? sender, EventArgs e) => _ui.Post(RefreshModules);

    private void OnNotificationsChanged(object? sender, EventArgs e) => _ui.Post(RefreshNotifications);

    private void OnSettingsChanged(object? sender, IslandSettings settings) => _ui.Post(() =>
    {
        // Mudanças vindas de outro lugar (ex.: menu da bandeja) aparecem aqui sem regravar.
        _loading = true;
        try
        {
            IsIslandVisible = settings.IsVisible;
            RefreshMonitors(settings);
            RefreshAccents(settings);
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
        _notifications.Changed -= OnNotificationsChanged;
        Behavior.Dispose();
        Privacy.Dispose();
        Shortcuts.Dispose();
        Storage.Dispose();
        Weather.Dispose();
        Spotify.Dispose();
    }
}

public sealed partial class ModuleSettingViewModel : ObservableObject
{
    private readonly Func<string, bool, Task> _setEnabled;

    public ModuleSettingViewModel(ModuleInfo info, IReadOnlyList<ModuleOptionViewModel> options, Func<string, bool, Task> setEnabled)
    {
        _setEnabled = setEnabled;
        Id = info.Id;
        Name = info.DisplayName;
        Description = info.Description;
        Options = options;
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

    public IReadOnlyList<ModuleOptionViewModel> Options { get; }

    /// <summary>Opções só fazem sentido para módulos disponíveis e ligados.</summary>
    public bool ShowOptions => Options.Count > 0 && Status == ModuleStatus.Active;

    public bool CanToggle { get; }

    /// <summary>Motivo de indisponibilidade ou falha.</summary>
    public string? Reason { get; }

    public bool HasReason => !string.IsNullOrWhiteSpace(Reason);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText), nameof(ShowOptions))]
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

/// <summary>Uma opção de módulo (interruptor ou controle deslizante).</summary>
public sealed partial class ModuleOptionViewModel : ObservableObject
{
    private readonly ModuleOption _option;
    private readonly Func<double, Task> _save;

    public ModuleOptionViewModel(ModuleOption option, double value, Func<double, Task> save)
    {
        _option = option;
        _save = save;
        _value = value;
    }

    public string Label => _option.Label;

    public string Description => _option.Description;

    public bool IsToggle => _option.Kind == ModuleOptionKind.Toggle;

    public bool IsSlider => _option.Kind == ModuleOptionKind.Slider;

    public double Minimum => _option.Minimum;

    public double Maximum => _option.Maximum;

    public double Step => _option.Step;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsOn), nameof(ValueText))]
    private double _value;

    public bool IsOn
    {
        get => Value >= 0.5;
        set => Value = value ? 1 : 0;
    }

    public string ValueText => $"{Value:0.##} {_option.Unit}".Trim();

    partial void OnValueChanged(double value) => _ = _save(_option.Normalize(value));
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

public sealed partial class AccentOptionViewModel : ObservableObject
{
    private readonly Action<string> _select;

    public AccentOptionViewModel(string name, string hex, bool isSelected, Action<string> select)
    {
        _select = select;
        Name = name;
        Hex = hex;
        _isSelected = isSelected;
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        brush.Freeze();
        Brush = brush;
    }

    public string Name { get; }

    public string Hex { get; }

    public Brush Brush { get; }

    [ObservableProperty]
    private bool _isSelected;

    partial void OnIsSelectedChanged(bool value)
    {
        if (value)
        {
            _select(Hex);
        }
    }
}

public sealed partial class IslandThemeOptionViewModel(IslandTheme theme, string name, Brush brush, bool isSelected, Action<IslandTheme> select) : ObservableObject
{
    public string Name { get; } = name;

    public Brush Brush { get; } = brush;

    [ObservableProperty]
    private bool _isSelected = isSelected;

    partial void OnIsSelectedChanged(bool value)
    {
        if (value)
        {
            select(theme);
        }
    }
}

/// <summary>Aplicativo que já enviou notificações: mostrar ou não na ilha.</summary>
/// <summary>Regras de notificação de um aplicativo: mostrar, só no histórico, prioridade e duração próprias.</summary>
public sealed partial class NotificationAppViewModel : ObservableObject
{
    private readonly NotificationPreferences _preferences;
    private readonly ILogger _logger;

    public NotificationAppViewModel(KnownNotificationApp app, bool shows, NotificationPreferences preferences, ILogger logger)
    {
        _preferences = preferences;
        _logger = logger;
        AppId = app.AppId;
        Name = app.Name;
        FollowsMode = app.Rule is null;
        _shows = shows;
        _historyOnly = app.HistoryOnly;
        _highPriority = app.HighPriority;
        _hasCustomDuration = app.DurationSeconds is not null;
        _durationSeconds = app.DurationSeconds ?? preferences.DurationSeconds;
    }

    public string AppId { get; }

    public string Name { get; }

    /// <summary>Sem escolha explícita: segue o modo (todos ou somente permitidos).</summary>
    public bool FollowsMode { get; }

    [ObservableProperty]
    private bool _shows;

    [ObservableProperty]
    private bool _historyOnly;

    [ObservableProperty]
    private bool _highPriority;

    [ObservableProperty]
    private bool _hasCustomDuration;

    [ObservableProperty]
    private double _durationSeconds;

    [ObservableProperty]
    private bool _isExpanded;

    partial void OnShowsChanged(bool value) => Guard(_preferences.SetRuleAsync(AppId, value));

    partial void OnHistoryOnlyChanged(bool value) => Guard(_preferences.SetHistoryOnlyAsync(AppId, value));

    partial void OnHighPriorityChanged(bool value) => Guard(_preferences.SetHighPriorityAsync(AppId, value));

    partial void OnHasCustomDurationChanged(bool value) => Guard(_preferences.SetAppDurationAsync(AppId, value ? DurationSeconds : null));

    partial void OnDurationSecondsChanged(double value)
    {
        if (HasCustomDuration)
        {
            Guard(_preferences.SetAppDurationAsync(AppId, value));
        }
    }

    [RelayCommand]
    private void ToggleExpanded() => IsExpanded = !IsExpanded;

    private async void Guard(Task task)
    {
        try
        {
            await task;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao salvar regra de notificação de {App}.", Name);
        }
    }
}
