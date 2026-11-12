using System.Windows;
using System.Windows.Media;
using System.Windows.Documents;
using System.Windows.Media.Animation;

namespace Dynasland.Modules.Common;

/// <summary>
/// Ícone de som do Volum: 7 barras que oscilam enquanto há som; mudo (ou sem volume) vira uma linha fina e apagada.
/// Segue a cor do texto herdada, como os outros ícones.
/// </summary>
public sealed class WaveIcon : FrameworkElement
{
    private const int BarCount = 7;
    private const double Thickness = 1.6;
    private const double MutedOpacity = 0.35;
    private static readonly Duration CycleDuration = new(TimeSpan.FromMilliseconds(1300));

    public static readonly DependencyProperty ForegroundProperty = TextElement.ForegroundProperty.AddOwner(
        typeof(WaveIcon),
        new FrameworkPropertyMetadata(Brushes.White, FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.Inherits));

    public static readonly DependencyProperty IsMutedProperty = DependencyProperty.Register(
        nameof(IsMuted), typeof(bool), typeof(WaveIcon), new FrameworkPropertyMetadata(false, OnStateChanged));

    public static readonly DependencyProperty LevelProperty = DependencyProperty.Register(
        nameof(Level), typeof(double), typeof(WaveIcon), new FrameworkPropertyMetadata(100.0, OnStateChanged));

    private static readonly DependencyProperty PhaseProperty = DependencyProperty.Register(
        nameof(Phase), typeof(double), typeof(WaveIcon), new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

    static WaveIcon() => DefaultStyleKeyProperty.OverrideMetadata(typeof(WaveIcon), new FrameworkPropertyMetadata(typeof(FrameworkElement)));

    public WaveIcon()
    {
        Width = 16;
        Height = 16;
        IsHitTestVisible = false;
        Loaded += (_, _) => UpdateAnimation();
        Unloaded += (_, _) => StopAnimation();
        IsVisibleChanged += (_, _) => UpdateAnimation();
    }

    public Brush Foreground
    {
        get => (Brush)GetValue(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    public bool IsMuted
    {
        get => (bool)GetValue(IsMutedProperty);
        set => SetValue(IsMutedProperty, value);
    }

    /// <summary>Volume de 0 a 100; zero também apaga a onda.</summary>
    public double Level
    {
        get => (double)GetValue(LevelProperty);
        set => SetValue(LevelProperty, value);
    }

    private double Phase => (double)GetValue(PhaseProperty);

    private bool IsSounding => !IsMuted && Level > 0;

    private static void OnStateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var icon = (WaveIcon)d;
        icon.UpdateAnimation();
        icon.InvalidateVisual();
    }

    private void UpdateAnimation()
    {
        if (IsSounding && IsLoaded && IsVisible && SystemParameters.ClientAreaAnimation)
        {
            var animation = new DoubleAnimation(0, 2 * Math.PI, CycleDuration) { RepeatBehavior = RepeatBehavior.Forever };
            Timeline.SetDesiredFrameRate(animation, 30);
            BeginAnimation(PhaseProperty, animation);
        }
        else
        {
            StopAnimation();
        }
    }

    private void StopAnimation() => BeginAnimation(PhaseProperty, null);

    protected override void OnRender(DrawingContext dc)
    {
        var brush = Foreground;
        var width = ActualWidth;
        var height = ActualHeight;
        var middle = height / 2;

        if (!IsSounding)
        {
            var flat = new Pen(brush, Thickness) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
            dc.PushOpacity(MutedOpacity);
            dc.DrawLine(flat, new Point(Thickness, middle), new Point(width - Thickness, middle));
            dc.Pop();
            return;
        }

        var pen = new Pen(brush, Thickness) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        var step = (width - Thickness * 2) / (BarCount - 1);
        for (var i = 0; i < BarCount; i++)
        {
            // Envoltória: barras do meio mais altas; o seno desloca cada barra no tempo.
            var envelope = 0.45 + 0.55 * Math.Sin(Math.PI * (i + 0.5) / BarCount);
            var swing = 0.3 + 0.7 * Math.Abs(Math.Sin(Phase + i * 0.9));
            var half = Math.Max(Thickness / 2, height * 0.5 * envelope * swing);
            var x = Thickness + step * i;
            dc.DrawLine(pen, new Point(x, middle - half), new Point(x, middle + half));
        }
    }
}
