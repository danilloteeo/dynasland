using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dynasland.Core.Interfaces;
using Dynasland.Core.Models;
using Microsoft.Extensions.Logging;

namespace Dynasland.App.ViewModels.Hub;

public sealed record NotificationAppFilter(string? AppId, string Name);

public sealed class NotificationItemViewModel(NotificationRecord record)
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    public long Id => record.Id;

    public string AppId => record.AppId;

    public string AppName => record.AppName;

    public string Title => string.IsNullOrWhiteSpace(record.Title) ? record.AppName : record.Title;

    public string Body => record.Body;

    public bool HasBody => !string.IsNullOrWhiteSpace(record.Body);

    public bool WasHidden => !record.WasShown;

    public string TimeText
    {
        get
        {
            var local = record.ReceivedAt.ToLocalTime();
            return local.Date == DateTime.Today ? local.ToString("'Hoje,' HH:mm", PtBr)
                : local.Date == DateTime.Today.AddDays(-1) ? local.ToString("'Ontem,' HH:mm", PtBr)
                : local.ToString("dd/MM HH:mm", PtBr);
        }
    }
}

/// <summary>Página da Central com o histórico de notificações recebidas.</summary>
public sealed partial class NotificationsPageViewModel(
    INotificationHistoryStore store,
    INotificationService notifications,
    IIslandSettingsService settings,
    IUiDispatcher ui,
    ILogger<NotificationsPageViewModel> logger) : ObservableObject, IHubPage
{
    private List<NotificationRecord> _all = [];

    public HubPage Page => HubPage.Notifications;

    public Func<string, bool>? Confirm { get; set; }

    public ObservableCollection<NotificationItemViewModel> Items { get; } = [];

    public ObservableCollection<NotificationAppFilter> Apps { get; } = [];

    [ObservableProperty]
    private NotificationAppFilter? _selectedApp;

    [ObservableProperty]
    private string _statusText = string.Empty;

    [ObservableProperty]
    private string _emptyText = string.Empty;

    [ObservableProperty]
    private bool _isEmpty = true;

    public string RetentionText => settings.Current.NotificationHistoryEnabled
        ? $"Guardadas por {settings.Current.NotificationHistoryDays} dias (altere em Configurações › Armazenamento)."
        : "O histórico de notificações está desligado em Configurações › Privacidade.";

    public async Task LoadAsync()
    {
        try
        {
            _all = [.. await store.ListAsync(500)];
            ui.Post(() =>
            {
                var selected = SelectedApp?.AppId;
                Apps.Clear();
                Apps.Add(new NotificationAppFilter(null, "Todos os aplicativos"));
                foreach (var app in _all.GroupBy(r => r.AppId).Select(g => new NotificationAppFilter(g.Key, g.First().AppName)).OrderBy(a => a.Name, StringComparer.CurrentCultureIgnoreCase))
                {
                    Apps.Add(app);
                }

                SelectedApp = Apps.FirstOrDefault(a => a.AppId == selected) ?? Apps[0];
                ApplyFilter();
                OnPropertyChanged(nameof(RetentionText));
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Falha ao carregar o histórico de notificações.");
            ui.Post(() => EmptyText = "Não foi possível carregar o histórico.");
        }
    }

    partial void OnSelectedAppChanged(NotificationAppFilter? value) => ApplyFilter();

    [RelayCommand]
    private void OpenApp(NotificationItemViewModel? item)
    {
        if (item is not null)
        {
            notifications.TryOpenApp(item.AppId);
        }
    }

    [RelayCommand]
    private async Task DeleteAsync(NotificationItemViewModel? item)
    {
        if (item is null)
        {
            return;
        }

        await store.DeleteAsync(item.Id);
        _all.RemoveAll(r => r.Id == item.Id);
        ApplyFilter();
    }

    [RelayCommand]
    private async Task ClearAsync()
    {
        if (Confirm?.Invoke("Apagar todo o histórico de notificações?") == false)
        {
            return;
        }

        await store.ClearAsync();
        await LoadAsync();
    }

    public void Dispose()
    {
    }

    private void ApplyFilter()
    {
        var appId = SelectedApp?.AppId;
        Items.Clear();
        foreach (var record in _all.Where(r => appId is null || r.AppId == appId))
        {
            Items.Add(new NotificationItemViewModel(record));
        }

        IsEmpty = Items.Count == 0;
        StatusText = Items.Count == 1 ? "1 notificação" : $"{Items.Count} notificações";
        EmptyText = settings.Current.NotificationHistoryEnabled
            ? "Nenhuma notificação guardada."
            : "O histórico de notificações está desligado.";
    }
}
