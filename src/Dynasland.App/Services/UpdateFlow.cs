using System.ComponentModel;
using System.IO;
using System.Net.Http;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dynasland.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace Dynasland.App.Services;

public enum UpdateStage
{
    /// <summary>Nenhuma versão nova conhecida.</summary>
    None,
    Available,
    Downloading,

    /// <summary>Instalador iniciado; o aplicativo está fechando para ser substituído.</summary>
    Installing,
    Failed,
}

/// <summary>
/// Atualização dentro do aplicativo: da versão oferecida ao instalador em execução. Um único fluxo é
/// compartilhado pela ilha e pela página Sobre, que apenas mostram o estado dele.
/// </summary>
public sealed partial class UpdateFlow(
    IUpdateInstaller installer,
    IShellService shell,
    IUiDispatcher ui,
    ILogger<UpdateFlow> logger) : ObservableObject
{
    private readonly IUiDispatcher _ui = ui;
    private UpdateCheckResult? _offer;

    /// <summary>O instalador foi iniciado: o aplicativo deve encerrar para ele trocar os arquivos.</summary>
    public event EventHandler? ExitRequested;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsAvailable), nameof(IsBusy), nameof(IsDownloading), nameof(HasFailed), nameof(Title), nameof(Detail), nameof(ActionLabel), nameof(StatusLine), nameof(HasStatusLine))]
    [NotifyCanExecuteChangedFor(nameof(InstallCommand))]
    private UpdateStage _stage;

    /// <summary>Fração baixada, de 0 a 1.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Percent), nameof(Detail), nameof(StatusLine), nameof(HasStatusLine))]
    private double _progress;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Detail), nameof(StatusLine), nameof(HasStatusLine))]
    private string? _version;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Detail), nameof(StatusLine), nameof(HasStatusLine))]
    private string? _error;

    /// <summary>Há uma versão nova e nada em andamento (inclui a nova tentativa depois de uma falha).</summary>
    public bool IsAvailable => Stage is UpdateStage.Available or UpdateStage.Failed;

    public bool IsBusy => Stage is UpdateStage.Downloading or UpdateStage.Installing;

    public bool IsDownloading => Stage == UpdateStage.Downloading;

    public bool HasFailed => Stage == UpdateStage.Failed;

    public int Percent => (int)Math.Round(Progress * 100);

    /// <summary>A versão instalada baixa e instala sozinha; a portátil só abre a página de download.</summary>
    public bool InstallsInApp => _offer?.Installer is not null && installer.CanInstall;

    public string Title => Stage switch
    {
        UpdateStage.Downloading => "Baixando atualização",
        UpdateStage.Installing => "Instalando atualização",
        UpdateStage.Failed => "Atualização não concluída",
        _ => "Atualização disponível",
    };

    public string Detail => Stage switch
    {
        UpdateStage.Downloading => $"Dynasland {Version} · {Percent}%",
        UpdateStage.Installing => "O Dynasland vai fechar e reabrir sozinho.",
        UpdateStage.Failed => Error ?? "Tente novamente.",
        _ => $"Dynasland {Version}",
    };

    /// <summary>Andamento em uma linha, para a página Sobre; vazio quando não há nada em andamento.</summary>
    public string StatusLine => IsBusy || HasFailed ? $"{Title}. {Detail}" : string.Empty;

    public bool HasStatusLine => StatusLine.Length > 0;

    public string ActionLabel => Stage == UpdateStage.Failed
        ? "Tentar de novo"
        : InstallsInApp ? "Atualizar agora" : "Baixar";

    /// <summary>Recebe o resultado de uma verificação. Não interfere em um download em andamento.</summary>
    public void Offer(UpdateCheckResult result)
    {
        if (IsBusy)
        {
            return;
        }

        if (result is { Status: UpdateStatus.UpdateAvailable, LatestVersion: { } version })
        {
            _offer = result;
            Version = version;
            Error = null;
            Stage = UpdateStage.Available;
            OnPropertyChanged(nameof(InstallsInApp));
            OnPropertyChanged(nameof(ActionLabel));
        }
        else if (result.Status == UpdateStatus.UpToDate)
        {
            _offer = null;
            Stage = UpdateStage.None;
        }
    }

    [RelayCommand(CanExecute = nameof(IsAvailable))]
    private async Task InstallAsync()
    {
        if (_offer is not { LatestVersion: { } version } offer)
        {
            return;
        }

        if (!InstallsInApp || offer.Installer is not { } asset)
        {
            OpenReleasePage();
            return;
        }

        Error = null;
        Progress = 0;
        Stage = UpdateStage.Downloading;
        try
        {
            var path = await installer.DownloadAsync(asset, new UiProgress(this));
            Progress = 1;
            Stage = UpdateStage.Installing;
            installer.Launch(path, version);
            ExitRequested?.Invoke(this, EventArgs.Empty);
        }
        catch (UpdateVerificationException ex)
        {
            logger.LogWarning("Atualização recusada: {Message}", ex.Message);
            Fail(ex.Message);
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or TaskCanceledException or UnauthorizedAccessException or Win32Exception)
        {
            logger.LogWarning(ex, "Falha ao baixar ou iniciar a atualização.");
            Fail(Stage == UpdateStage.Installing ? "Não foi possível iniciar o instalador." : "Não foi possível baixar. Verifique a conexão.");
        }
    }

    [RelayCommand]
    private void OpenReleasePage()
    {
        if (_offer?.ReleaseUrl is { } url)
        {
            shell.OpenUrl(url);
        }
    }

    private void Fail(string message)
    {
        Error = message;
        Stage = UpdateStage.Failed;
    }

    /// <summary>Leva o progresso para a thread de interface, só quando o percentual inteiro muda.</summary>
    private sealed class UiProgress(UpdateFlow flow) : IProgress<double>
    {
        private int _lastPercent = -1;

        public void Report(double value)
        {
            var percent = (int)(value * 100);
            if (Interlocked.Exchange(ref _lastPercent, percent) != percent)
            {
                flow._ui.Post(() =>
                {
                    if (flow.Stage == UpdateStage.Downloading)
                    {
                        flow.Progress = value;
                    }
                });
            }
        }
    }
}
