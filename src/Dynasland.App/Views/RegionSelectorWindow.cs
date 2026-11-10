using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Dynasland.Core.Models;

namespace Dynasland.App.Views;

/// <summary>
/// Sobreposição de tela inteira (todos os monitores) com a tela congelada: o usuário arrasta para escolher
/// a área; Esc ou botão direito cancela. As coordenadas são convertidas pela fração da janela, então
/// funcionam mesmo com monitores de escalas diferentes.
/// </summary>
public sealed class RegionSelectorWindow : Window
{
    private readonly PixelRect _desktop;
    private readonly int _imageWidth;
    private readonly int _imageHeight;
    private readonly Canvas _canvas = new();
    private readonly Rectangle _selection;
    private readonly RectangleGeometry _hole = new();
    private readonly TextBlock _size;
    private Point? _start;

    public RegionSelectorWindow(byte[] png, int imageWidth, int imageHeight, PixelRect desktop)
    {
        _desktop = desktop;
        _imageWidth = imageWidth;
        _imageHeight = imageHeight;

        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        Topmost = true;
        Cursor = Cursors.Cross;
        Title = "Selecionar área";
        Background = Brushes.Black;

        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.StreamSource = new MemoryStream(png);
        image.EndInit();
        image.Freeze();

        var dim = new System.Windows.Shapes.Path
        {
            Fill = new SolidColorBrush(Color.FromArgb(120, 0, 0, 0)),
            IsHitTestVisible = false,
        };
        _selection = new Rectangle { Stroke = Brushes.White, StrokeThickness = 1, StrokeDashArray = [4, 3], Visibility = Visibility.Collapsed, IsHitTestVisible = false };
        _size = new TextBlock
        {
            Foreground = Brushes.White,
            Background = new SolidColorBrush(Color.FromArgb(180, 0, 0, 0)),
            Padding = new Thickness(6, 2, 6, 2),
            FontSize = 12,
            Visibility = Visibility.Collapsed,
            IsHitTestVisible = false,
        };
        var hint = new TextBlock
        {
            Text = "Arraste para selecionar · Esc cancela",
            Foreground = Brushes.White,
            Background = new SolidColorBrush(Color.FromArgb(180, 0, 0, 0)),
            Padding = new Thickness(10, 4, 10, 4),
            FontSize = 13,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 24, 0, 0),
            IsHitTestVisible = false,
        };

        var root = new Grid();
        root.Children.Add(new Image { Source = image, Stretch = Stretch.Fill });
        root.Children.Add(dim);
        root.Children.Add(_canvas);
        root.Children.Add(hint);
        _canvas.Background = Brushes.Transparent;
        _canvas.Children.Add(_selection);
        _canvas.Children.Add(_size);
        Content = root;

        // Escurece tudo, exceto a área selecionada.
        root.SizeChanged += (_, _) =>
        {
            dim.Data = new CombinedGeometry(GeometryCombineMode.Exclude, new RectangleGeometry(new Rect(0, 0, root.ActualWidth, root.ActualHeight)), _hole);
        };

        MouseLeftButtonDown += OnMouseDown;
        MouseMove += OnMouseMove;
        MouseLeftButtonUp += OnMouseUp;
        MouseRightButtonUp += (_, _) => Finish(null);
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                Finish(null);
            }
        };
        Deactivated += (_, _) => Finish(null);
    }

    public TaskCompletionSource<PixelRect?> Result { get; } = new();

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        // Posição e tamanho em pixels físicos cobrindo todos os monitores.
        var hwnd = new WindowInteropHelper(this).Handle;
        SetWindowPos(hwnd, new IntPtr(-1), _desktop.Left, _desktop.Top, _desktop.Width, _desktop.Height, 0x0040);
    }

    private void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        _start = e.GetPosition(_canvas);
        _selection.Visibility = Visibility.Visible;
        _size.Visibility = Visibility.Visible;
        CaptureMouse();
        Update(_start.Value);
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (_start is not null)
        {
            Update(e.GetPosition(_canvas));
        }
    }

    private void OnMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_start is not { } start)
        {
            return;
        }

        ReleaseMouseCapture();
        var rect = new Rect(start, e.GetPosition(_canvas));
        Finish(ToPixels(rect));
    }

    private void Update(Point current)
    {
        var rect = new Rect(_start!.Value, current);
        Canvas.SetLeft(_selection, rect.X);
        Canvas.SetTop(_selection, rect.Y);
        _selection.Width = rect.Width;
        _selection.Height = rect.Height;
        _hole.Rect = rect;

        var pixels = ToPixels(rect);
        _size.Text = $"{pixels.Width} × {pixels.Height}";
        Canvas.SetLeft(_size, rect.X);
        Canvas.SetTop(_size, Math.Max(0, rect.Y - 24));
    }

    private PixelRect ToPixels(Rect rect)
    {
        var scaleX = _imageWidth / Math.Max(1, _canvas.ActualWidth);
        var scaleY = _imageHeight / Math.Max(1, _canvas.ActualHeight);
        var left = (int)Math.Round(rect.Left * scaleX);
        var top = (int)Math.Round(rect.Top * scaleY);
        var right = (int)Math.Round(rect.Right * scaleX);
        var bottom = (int)Math.Round(rect.Bottom * scaleY);
        return new PixelRect(
            _desktop.Left + Math.Clamp(left, 0, _imageWidth),
            _desktop.Top + Math.Clamp(top, 0, _imageHeight),
            _desktop.Left + Math.Clamp(right, 0, _imageWidth),
            _desktop.Top + Math.Clamp(bottom, 0, _imageHeight));
    }

    private void Finish(PixelRect? result)
    {
        if (Result.TrySetResult(result))
        {
            Close();
        }
    }

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hwnd, IntPtr insertAfter, int x, int y, int width, int height, uint flags);
}
