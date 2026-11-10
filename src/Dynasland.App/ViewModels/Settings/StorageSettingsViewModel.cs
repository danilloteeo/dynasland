using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dynasland.Core.Interfaces;
using Dynasland.Core.Models;
using Dynasland.Core.Modules;
using Dynasland.Core.Shortcuts;
using Dynasland.Infrastructure.Logging;
using Dynasland.Modules.Notifications;
using Microsoft.Extensions.Logging;

namespace Dynasland.App.ViewModels.Settings;

/// <summary>Categoria Armazenamento: tamanho do banco, retenção dos históricos, cópia de segurança e pastas.</summary>
public sealed partial class StorageSettingsViewModel : ObservableObject, IDisposable
{
    private readonly IStorageMaintenance _storage;
    private readonly IIslandSettingsService _settings;
    private readonly IModuleOptions _moduleOptions;
    private readonly IGlobalShortcutRegistry _shortcuts;
    private readonly NotificationPreferences _notifications;
    private readonly IShellService _shell;
    private readonly IUiDispatcher _ui;
    private readonly ILogger<StorageSettingsViewModel> _logger;
    private bool _loading;

    public StorageSettingsViewModel(
        IStorageMaintenance storage,
        IIslandSettingsService settings,
        IModuleOptions moduleOptions,
        IGlobalShortcutRegistry shortcuts,
        NotificationPreferences notifications,
        IShellService shell,
        IUiDispatcher ui,
        ILogger<StorageSettingsViewModel> logger)
    {
        _storage = storage;
        _settings = settings;
        _moduleOptions = moduleOptions;
        _shortcuts = shortcuts;
        _notifications = notifications;
        _shell = shell;
        _ui = ui;
        _logger = logger;
        DatabasePath = storage.DatabasePath;
        Load(settings.Current);
        RefreshSize();
        settings.Changed += OnSettingsChanged;
    }

    public string DatabasePath { get; }

    [ObservableProperty]
    private string _databaseSize = string.Empty;

    [ObservableProperty]
    private double _clipboardMaxItems;

    [ObservableProperty]
    private double _notificationHistoryDays;

    [ObservableProperty]
    private string? _lastAction;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CompactCommand))]
    private bool _isBusy;

    partial void OnClipboardMaxItemsChanged(double value) => Save(s => s with { ClipboardMaxItems = (int)Math.Round(value) });

    partial void OnNotificationHistoryDaysChanged(double value) => Save(s => s with { NotificationHistoryDays = (int)Math.Round(value) });

    [RelayCommand(CanExecute = nameof(CanRun))]
    private async Task CompactAsync()
    {
        IsBusy = true;
        try
        {
            await _storage.ApplyRetentionAsync();
            await _storage.CompactAsync();
            LastAction = "Banco compactado.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao compactar o banco.");
            LastAction = "Não foi possível compactar agora.";
        }
        finally
        {
            IsBusy = false;
            RefreshSize();
        }
    }

    private bool CanRun() => !IsBusy;

    /// <summary>Exporta as configurações para o arquivo escolhido na janela.</summary>
    public async Task ExportAsync(string path)
    {
        try
        {
            await _storage.ExportSettingsAsync(path);
            LastAction = $"Configurações exportadas para {Path.GetFileName(path)}.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao exportar configurações.");
            LastAction = "Não foi possível exportar as configurações.";
        }
    }

    /// <summary>Importa as configurações e recarrega todos os serviços que as leem.</summary>
    public async Task ImportAsync(string path)
    {
        try
        {
            var count = await _storage.ImportSettingsAsync(path);
            await _settings.LoadAsync();
            await _moduleOptions.LoadAsync();
            await _shortcuts.LoadAsync();
            await _notifications.LoadAsync();
            LastAction = $"{count} configurações importadas.";
        }
        catch (InvalidDataException ex)
        {
            LastAction = ex.Message;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao importar configurações.");
            LastAction = "Não foi possível importar as configurações.";
        }
    }

    [RelayCommand]
    private void OpenDataFolder() => _shell.Open(Path.GetDirectoryName(DatabasePath)!);

    [RelayCommand]
    private void OpenLogsFolder()
    {
        var folder = Path.GetDirectoryName(FileLoggerProvider.DefaultPath)!;
        Directory.CreateDirectory(folder);
        _shell.Open(folder);
    }

    public void Dispose() => _settings.Changed -= OnSettingsChanged;

    private void RefreshSize()
    {
        var bytes = _storage.GetDatabaseSizeBytes();
        DatabaseSize = bytes < 1024 * 1024 ? $"{bytes / 1024.0:0} KB" : $"{bytes / (1024.0 * 1024):0.0} MB";
    }

    private void OnSettingsChanged(object? sender, IslandSettings settings) => _ui.Post(() => Load(settings));

    private void Load(IslandSettings s)
    {
        _loading = true;
        try
        {
            ClipboardMaxItems = s.ClipboardMaxItems;
            NotificationHistoryDays = s.NotificationHistoryDays;
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
            await _storage.ApplyRetentionAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao salvar armazenamento.");
        }
    }
}
