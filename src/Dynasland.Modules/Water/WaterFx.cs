using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace Dynasland.Modules.Water;

/// <summary>Peças comuns da água animada (<see cref="WaterFillMeter"/> e <see cref="WaterBodyMeter"/>): cores, ondas e gotinhas.</summary>
internal static class WaterFx
{
    public static readonly Color WaterColor = Color.FromRgb(0x3A, 0xA7, 0xFF);
    public static readonly Brush WaterBrush = Frozen(new LinearGradientBrush(WaterColor, Color.FromRgb(0x1E, 0x6F, 0xD9), 90));
    public static readonly Brush BackWaterBrush = Frozen(new SolidColorBrush(Color.FromArgb(0x8C, 0x5F, 0xB8, 0xFF)));
    public static readonly Brush DropBrush = Frozen(new RadialGradientBrush(Color.FromRgb(0x9A, 0xD4, 0xFF), WaterColor) { GradientOrigin = new Point(0.35, 0.35) });

    private static readonly TimeSpan DropDuration = TimeSpan.FromMilliseconds(750);

    public static bool AnimationsEnabled => SystemParameters.ClientAreaAnimation;

    public static double Clamp01(double value) => double.IsNaN(value) ? 0 : Math.Clamp(value, 0, 1);

    /// <summary>
    /// Superfície em senoide com a linha média em y = <paramref name="offset"/>, de x <paramref name="left"/> a
    /// <paramref name="right"/>, fechada embaixo em y = <paramref name="bottom"/>.
    /// </summary>
    public static Geometry WaveGeometry(double offset, double amplitude, double period, double left, double right, double bottom, double step)
    {
        var points = new PointCollection();
        for (var x = left; x <= right + 0.0001; x += step)
        {
            points.Add(new Point(x, offset + amplitude * Math.Sin(x / period * 2 * Math.PI)));
        }

        points.Add(new Point(right, bottom));
        points.Add(new Point(left, bottom));
        var figure = new PathFigure(new Point(left, offset), [new PolyLineSegment(points, false)], closed: true);
        var geometry = new PathGeometry([figure]);
        geometry.Freeze();
        return geometry;
    }

    /// <summary>Desliza a camada de onda até <paramref name="to"/> em loop.</summary>
    public static AnimationClock StartWave(TranslateTransform shift, double to, TimeSpan duration)
    {
        var clock = new DoubleAnimation(0, to, duration) { RepeatBehavior = RepeatBehavior.Forever }.CreateClock();
        shift.ApplyAnimationClock(TranslateTransform.XProperty, clock);
        return clock;
    }

    public static void StopWave(ref AnimationClock? clock, TranslateTransform shift)
    {
        clock?.Controller?.Stop();
        shift.ApplyAnimationClock(TranslateTransform.XProperty, null);
        clock = null;
    }

    /// <summary>Pulso 1 → <paramref name="peak"/> → 1.</summary>
    public static void Pulse(ScaleTransform scale, double peak, TimeSpan duration)
    {
        var animation = new DoubleAnimationUsingKeyFrames { Duration = duration };
        animation.KeyFrames.Add(new EasingDoubleKeyFrame(peak, KeyTime.FromPercent(0.4), new QuadraticEase { EasingMode = EasingMode.EaseOut }));
        animation.KeyFrames.Add(new EasingDoubleKeyFrame(1, KeyTime.FromPercent(1), new QuadraticEase { EasingMode = EasingMode.EaseIn }));
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, animation);
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, animation);
    }

    public static void StopPulse(ScaleTransform scale)
    {
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
    }

    /// <summary>
    /// Gotinha que sai de (<paramref name="x"/>, <paramref name="y"/>), sobe <paramref name="rise"/>, desvia
    /// <paramref name="drift"/> na horizontal e some; sai do Canvas ao terminar.
    /// </summary>
    public static void LaunchDrop(Canvas effects, Random random, double x, double y, double size, double rise, double drift, TimeSpan begin)
    {
        var drop = new Ellipse { Width = size, Height = size, Fill = DropBrush, Opacity = 0, IsHitTestVisible = false };
        Canvas.SetLeft(drop, x - size / 2);
        Canvas.SetTop(drop, y - size / 2);
        var move = new TranslateTransform();
        drop.RenderTransform = move;
        effects.Children.Add(drop);

        var duration = DropDuration + TimeSpan.FromMilliseconds(random.Next(-80, 120));
        var horizontal = new DoubleAnimation(0, drift, duration) { BeginTime = begin };
        var vertical = new DoubleAnimationUsingKeyFrames { BeginTime = begin, Duration = duration };
        vertical.KeyFrames.Add(new EasingDoubleKeyFrame(-rise, KeyTime.FromPercent(0.5), new QuadraticEase { EasingMode = EasingMode.EaseOut }));
        vertical.KeyFrames.Add(new EasingDoubleKeyFrame(-rise * 0.55, KeyTime.FromPercent(1), new QuadraticEase { EasingMode = EasingMode.EaseIn }));
        var fade = new DoubleAnimationUsingKeyFrames { BeginTime = begin, Duration = duration };
        fade.KeyFrames.Add(new LinearDoubleKeyFrame(1, KeyTime.FromPercent(0.1)));
        fade.KeyFrames.Add(new LinearDoubleKeyFrame(1, KeyTime.FromPercent(0.5)));
        fade.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromPercent(1)));
        fade.Completed += (_, _) => RemoveDrop(effects, drop);

        move.BeginAnimation(TranslateTransform.XProperty, horizontal);
        move.BeginAnimation(TranslateTransform.YProperty, vertical);
        drop.BeginAnimation(UIElement.OpacityProperty, fade);
    }

    public static void ClearDrops(Canvas effects)
    {
        foreach (var drop in effects.Children.OfType<Ellipse>().ToList())
        {
            RemoveDrop(effects, drop);
        }

        effects.Children.Clear();
    }

    private static void RemoveDrop(Canvas effects, Ellipse drop)
    {
        drop.BeginAnimation(UIElement.OpacityProperty, null);
        if (drop.RenderTransform is TranslateTransform move)
        {
            move.BeginAnimation(TranslateTransform.XProperty, null);
            move.BeginAnimation(TranslateTransform.YProperty, null);
        }

        effects.Children.Remove(drop);
    }

    private static Brush Frozen(Brush brush)
    {
        brush.Freeze();
        return brush;
    }
}
