using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dynasland.Core.Activities;
using Dynasland.Core.Layout;
using Dynasland.Core.Models;
using Dynasland.Modules.Common;

namespace Dynasland.Modules.Capture;

/// <summary>Painel do Capture Center: escolha do tipo de captura e da gravação.</summary>
public sealed partial class CapturePanelContent(Action region, Action screen, Action window, Action record, Action openHistory) : ObservableObject, IActivityContent
{
    [ObservableProperty]
    private string _recordLabel = "Gravar tela";

    [ObservableProperty]
    private string? _message;

    public bool HasMessage => Message is not null;

    partial void OnMessageChanged(string? value) => OnPropertyChanged(nameof(HasMessage));

    public string RegionGlyph => "";

    public string ScreenGlyph => Glyphs.Monitor;

    public string WindowGlyph => "";

    public string RecordGlyph => Glyphs.Record;

    public string HistoryGlyph => Glyphs.History;

    public IslandDimensions? GetPreferredDimensions(IslandState state) =>
        state == IslandState.Interactive ? new IslandDimensions(410, HasMessage ? 150 : 128, 34) : null;

    [RelayCommand]
    private void Region() => region();

    [RelayCommand]
    private void Screen() => screen();

    [RelayCommand]
    private void Window() => window();

    [RelayCommand]
    private void Record() => record();

    [RelayCommand]
    private void OpenHistory() => openHistory();
}

/// <summary>Captura concluída: miniatura e ações (copiar, abrir, mostrar na pasta).</summary>
public sealed partial class CaptureResultContent(string path, ImageSource? thumbnail, string title, string detail, Func<Task<bool>>? copy, Action open, Action reveal) : ObservableObject, IActivityContent
{
    public string Path => path;

    public ImageSource? Thumbnail => thumbnail;

    public bool HasThumbnail => thumbnail is not null;

    public string Title => title;

    public string Detail => detail;

    public bool CanCopy => copy is not null;

    public string Glyph => HasThumbnail ? Glyphs.Camera : Glyphs.Record;

    [ObservableProperty]
    private string _copyLabel = "Copiar";

    public IslandDimensions? GetPreferredDimensions(IslandState state) => state switch
    {
        IslandState.Expanded => new IslandDimensions(360, 72, 30),
        IslandState.Interactive => new IslandDimensions(400, 128, 34),
        _ => null,
    };

    [RelayCommand]
    private async Task CopyAsync()
    {
        if (copy is not null)
        {
            CopyLabel = await copy() ? "Copiado" : "Falhou";
        }
    }

    [RelayCommand]
    private void Open() => open();

    [RelayCommand]
    private void Reveal() => reveal();
}

/// <summary>Gravação em andamento: tempo, pausa e parar. Não pode ser fechada enquanto grava.</summary>
public sealed partial class RecordingContent(Action pauseResume, Action stop) : ObservableObject, IActivityContent
{
    [ObservableProperty]
    private string _elapsedText = "00:00";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PauseResumeGlyph), nameof(StateText))]
    private bool _isPaused;

    [ObservableProperty]
    private string _targetText = string.Empty;

    public string PauseResumeGlyph => IsPaused ? Glyphs.Play : Glyphs.Pause;

    public string StopGlyph => Glyphs.Stop;

    public string StateText => IsPaused ? "Pausado" : "Gravando";

    public IslandDimensions? GetPreferredDimensions(IslandState state) => state switch
    {
        IslandState.Compact => new IslandDimensions(210, 34, 17),
        IslandState.Expanded => new IslandDimensions(300, 56, 28),
        IslandState.Interactive => new IslandDimensions(360, 112, 32),
        _ => null,
    };

    [RelayCommand]
    private void PauseResume() => pauseResume();

    [RelayCommand]
    private void Stop() => stop();
}

public static class CaptureImages
{
    /// <summary>Recorta uma área de uma imagem PNG e devolve outro PNG.</summary>
    public static byte[] CropPng(byte[] png, int x, int y, int width, int height)
    {
        var decoder = new PngBitmapDecoder(new MemoryStream(png), BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        var cropped = new CroppedBitmap(decoder.Frames[0], new System.Windows.Int32Rect(x, y, width, height));
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(cropped));
        using var stream = new MemoryStream();
        encoder.Save(stream);
        return stream.ToArray();
    }
}
