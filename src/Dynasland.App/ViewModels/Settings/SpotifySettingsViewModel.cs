using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dynasland.Core.Interfaces;

namespace Dynasland.App.ViewModels.Settings;

/// <summary>Conexão com a conta do Spotify (fila "A seguir" no painel de mídia).</summary>
public sealed partial class SpotifySettingsViewModel : ObservableObject, IDisposable
{
    public const string DashboardUrl = "https://developer.spotify.com/dashboard";

    private readonly ISpotifyService _spotify;
    private readonly IIslandSettingsService _settings;
    private readonly IShellService _shell;
    private readonly IUiDispatcher _ui;

    public SpotifySettingsViewModel(ISpotifyService spotify, IIslandSettingsService settings, IShellService shell, IUiDispatcher ui)
    {
        _spotify = spotify;
        _settings = settings;
        _shell = shell;
        _ui = ui;
        _clientId = settings.Current.SpotifyClientId ?? string.Empty;
        spotify.Changed += OnChanged;
        Refresh();
    }

    public string RedirectUri => _spotify.RedirectUri;

    [ObservableProperty]
    private string _clientId;

    [ObservableProperty]
    private string _statusText = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanConnect))]
    private bool _isConnected;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanConnect))]
    private bool _isBusy;

    [ObservableProperty]
    private string? _message;

    public bool CanConnect => !IsConnected && !IsBusy;

    [RelayCommand]
    private void OpenDashboard() => _shell.OpenUrl(DashboardUrl);

    [RelayCommand]
    private void CopyRedirectUri()
    {
        try
        {
            Clipboard.SetText(RedirectUri);
            Message = "Endereço copiado.";
        }
        catch (System.Runtime.InteropServices.ExternalException)
        {
            Message = "Não foi possível copiar agora; selecione o endereço e copie.";
        }
    }

    [RelayCommand]
    private async Task ConnectAsync()
    {
        var clientId = ClientId.Trim();
        if (clientId.Length < 16)
        {
            Message = "Cole o Client ID do seu app do Spotify (32 caracteres).";
            return;
        }

        if (_settings.Current.SpotifyClientId != clientId)
        {
            await _settings.UpdateAsync(s => s with { SpotifyClientId = clientId });
        }

        IsBusy = true;
        Message = "Autorize o Dynasland na página do Spotify que abriu no navegador…";
        Message = await _spotify.ConnectAsync() ?? "Conectado. A fila aparece no painel do Spotify na ilha.";
        IsBusy = false;
    }

    [RelayCommand]
    private async Task DisconnectAsync()
    {
        await _spotify.DisconnectAsync();
        Message = "Conta desconectada.";
    }

    public void Dispose() => _spotify.Changed -= OnChanged;

    private void OnChanged(object? sender, EventArgs e) => _ui.Post(Refresh);

    private void Refresh()
    {
        IsConnected = _spotify.State == SpotifyConnectionState.Connected;
        StatusText = _spotify.State switch
        {
            SpotifyConnectionState.Connected => _spotify.AccountName is { } name ? $"Conectado como {name}" : "Conectado",
            SpotifyConnectionState.Connecting => "Aguardando a autorização no navegador…",
            SpotifyConnectionState.NotConfigured => "Não configurado",
            _ => "Desconectado",
        };
    }
}
