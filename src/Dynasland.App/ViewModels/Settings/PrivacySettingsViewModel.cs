using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dynasland.Core.Interfaces;
using Dynasland.Core.Models;
using Microsoft.Extensions.Logging;

namespace Dynasland.App.ViewModels.Settings;

/// <summary>Categoria Privacidade: o que o Dynasland guarda e como apagar.</summary>
public sealed partial class PrivacySettingsViewModel : ObservableObject, IDisposable
{
    private readonly IIslandSettingsService _settings;
    private readonly IClipboardHistory _clipboard;
    private readonly INotificationHistoryStore _notifications;
    private readonly IFocusSessionStore _focus;
    private readonly IUiDispatcher _ui;
    private readonly ILogger<PrivacySettingsViewModel> _logger;
    private bool _loading;

    public PrivacySettingsViewModel(
        IIslandSettingsService settings,
        IClipboardHistory clipboard,
        INotificationHistoryStore notifications,
        IFocusSessionStore focus,
        IUiDispatcher ui,
        ILogger<PrivacySettingsViewModel> logger)
    {
        _settings = settings;
        _clipboard = clipboard;
        _notifications = notifications;
        _focus = focus;
        _ui = ui;
        _logger = logger;
        Load(settings.Current);
        settings.Changed += OnSettingsChanged;
    }

    /// <summary>Confirmação das exclusões, fornecida pela janela.</summary>
    public Func<string, bool>? Confirm { get; set; }

    [ObservableProperty]
    private bool _clipboardHistoryEnabled;

    [ObservableProperty]
    private bool _clipboardStoreImages;

    [ObservableProperty]
    private bool _notificationHistoryEnabled;

    [ObservableProperty]
    private string? _lastAction;

    partial void OnClipboardHistoryEnabledChanged(bool value) => Save(s => s with { ClipboardHistoryEnabled = value });

    partial void OnClipboardStoreImagesChanged(bool value) => Save(s => s with { ClipboardStoreImages = value });

    partial void OnNotificationHistoryEnabledChanged(bool value) => Save(s => s with { NotificationHistoryEnabled = value });

    [RelayCommand]
    private async Task ClearClipboardAsync()
    {
        if (Confirm?.Invoke("Apagar todo o histórico da área de transferência, inclusive os favoritos?") == false)
        {
            return;
        }

        await _clipboard.ClearAsync(keepFavorites: false);
        LastAction = "Histórico da área de transferência apagado.";
    }

    [RelayCommand]
    private async Task ClearNotificationsAsync()
    {
        if (Confirm?.Invoke("Apagar todo o histórico de notificações?") == false)
        {
            return;
        }

        await _notifications.ClearAsync();
        LastAction = "Histórico de notificações apagado.";
    }

    [RelayCommand]
    private async Task ClearFocusAsync()
    {
        if (Confirm?.Invoke("Apagar todo o histórico de foco?") == false)
        {
            return;
        }

        await _focus.ClearAsync();
        LastAction = "Histórico de foco apagado.";
    }

    public void Dispose() => _settings.Changed -= OnSettingsChanged;

    private void OnSettingsChanged(object? sender, IslandSettings settings) => _ui.Post(() => Load(settings));

    private void Load(IslandSettings s)
    {
        _loading = true;
        try
        {
            ClipboardHistoryEnabled = s.ClipboardHistoryEnabled;
            ClipboardStoreImages = s.ClipboardStoreImages;
            NotificationHistoryEnabled = s.NotificationHistoryEnabled;
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
            _logger.LogError(ex, "Falha ao salvar privacidade.");
        }
    }
}
