using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dynasland.Core.Interfaces;
using Dynasland.Core.Models;
using Dynasland.Modules.Capture;
using Dynasland.Modules.Common;
using Microsoft.Extensions.Logging;

namespace Dynasland.App.ViewModels.Hub;

public sealed class CaptureItemViewModel(CaptureRecord record, ImageSource? thumbnail)
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    public CaptureRecord Record => record;

    public ImageSource? Thumbnail => thumbnail;

    public bool HasThumbnail => thumbnail is not null;

    public bool IsRecording => record.Kind == CaptureKind.Recording;

    public string Name => Path.GetFileName(record.Path);

    public bool Exists => File.Exists(record.Path);

    public string Detail
    {
        get
        {
            var size = record.SizeBytes < 1024 * 1024 ? $"{record.SizeBytes / 1024.0:0} KB" : $"{record.SizeBytes / (1024.0 * 1024):0.0} MB";
            var duration = record.DurationSeconds is { } seconds ? $" · {TextFormat.Duration(TimeSpan.FromSeconds(seconds))}" : string.Empty;
            var missing = Exists ? string.Empty : " · arquivo removido";
            return $"{record.CreatedAt.ToLocalTime().ToString("dd/MM/yyyy HH:mm", PtBr)} · {size}{duration}{missing}";
        }
    }
}

/// <summary>Página Capturas da Central: capturas e gravações recentes, com abrir, mostrar na pasta e excluir.</summary>
public sealed partial class CapturesPageViewModel(
    ICaptureHistoryStore store,
    IShellService shell,
    IUiDispatcher ui,
    ILogger<CapturesPageViewModel> logger) : ObservableObject, IHubPage
{
    private const int ThumbnailWidth = 160;

    public HubPage Page => HubPage.Captures;

    public Func<string, bool>? Confirm { get; set; }

    public ObservableCollection<CaptureItemViewModel> Items { get; } = [];

    [ObservableProperty]
    private bool _isEmpty = true;

    public string PicturesFolder => CaptureModule.PicturesFolder;

    public string VideosFolder => CaptureModule.VideosFolder;

    public async Task LoadAsync()
    {
        try
        {
            var records = await store.ListAsync(100);
            var items = await Task.Run(() => records.Select(r => new CaptureItemViewModel(r, LoadThumbnail(r))).ToList());
            ui.Post(() =>
            {
                Items.Clear();
                foreach (var item in items)
                {
                    Items.Add(item);
                }

                IsEmpty = Items.Count == 0;
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Falha ao carregar as capturas.");
        }
    }

    [RelayCommand]
    private void Open(CaptureItemViewModel? item)
    {
        if (item is { Exists: true })
        {
            shell.Open(item.Record.Path);
        }
    }

    [RelayCommand]
    private void Reveal(CaptureItemViewModel? item)
    {
        if (item is { Exists: true })
        {
            shell.RevealInFolder(item.Record.Path);
        }
    }

    [RelayCommand]
    private async Task DeleteAsync(CaptureItemViewModel? item)
    {
        if (item is null)
        {
            return;
        }

        if (item.Exists)
        {
            if (Confirm?.Invoke($"Excluir “{item.Name}”? O arquivo vai para a lixeira.") == false)
            {
                return;
            }

            try
            {
                Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(item.Record.Path, Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs, Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or OperationCanceledException)
            {
                logger.LogWarning(ex, "Não foi possível excluir {Path}.", item.Record.Path);
                return;
            }
        }

        await store.DeleteAsync(item.Record.Id);
        await LoadAsync();
    }

    [RelayCommand]
    private void OpenPictures() => OpenFolder(PicturesFolder);

    [RelayCommand]
    private void OpenVideos() => OpenFolder(VideosFolder);

    public void Dispose()
    {
    }

    private void OpenFolder(string folder)
    {
        Directory.CreateDirectory(folder);
        shell.Open(folder);
    }

    private static ImageSource? LoadThumbnail(CaptureRecord record)
    {
        if (record.Kind != CaptureKind.Screenshot || !File.Exists(record.Path))
        {
            return null;
        }

        try
        {
            return ImageDecoder.Decode(File.ReadAllBytes(record.Path), ThumbnailWidth);
        }
        catch (IOException)
        {
            return null;
        }
    }
}
