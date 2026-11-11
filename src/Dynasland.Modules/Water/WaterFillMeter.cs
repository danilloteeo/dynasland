using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using System.Windows.Threading;
using Dynasland.Modules.Common;

namespace Dynasland.Modules.Water;

/// <summary>
/// Gota d'água (contorno de <see cref="VectorIcons.Water"/>) que enche de <see cref="From"/> a <see cref="To"/>:
/// nível com easing, onda deslizando na superfície, pulso final e gotinhas subindo; com <see cref="Celebrate"/>,
/// brilho azul e mais gotas. O traço da gota usa o Foreground herdado, como o ícone vetorial.
/// </summary>
/// <remarks>
/// Anima ao carregar e de novo quando From/To/Celebrate mudam enquanto carregado (DataTemplate reaproveitado
/// para outro conteúdo). Fora da árvore visual, ou com <see cref="SystemParameters.ClientAreaAnimation"/>
/// desligado, mostra direto o nível final, sem onda nem efeitos. Ao descarregar para e limpa tudo.
/// </remarks>
public sealed class WaterFillMeter : UserControl
{
    // Grade 24×24 do ícone: a base circular da gota fica em y 21,75 e a ponta em y 2,25.
    private const double BottomLevel = 21.75;
    private const double TopLevel = 4.5;
    private const double WaveAmplitude = 0.7;
    private const double WavePeriod = 6;
    private const double GlowOpacity = 0.75;

    private static readonly TimeSpan LevelDuration = TimeSpan.FromMilliseconds(900);
    private static readonly TimeSpan PulseDuration = TimeSpan.FromMilliseconds(350);
    private static readonly TimeSpan DropDuration = TimeSpan.FromMilliseconds(750);
    private static readonly Color WaterColor = Color.FromRgb(0x3A, 0xA7, 0xFF);
    private static readonly Brush WaterBrush = Frozen(new LinearGradientBrush(WaterColor, Color.FromRgb(0x1E, 0x6F, 0xD9), 90));
    private static readonly Brush BackWaterBrush = Frozen(new SolidColorBrush(Color.FromArgb(0x8C, 0x5F, 0xB8, 0xFF)));
    private static readonly Brush DropBrush = Frozen(new RadialGradientBrush(Color.FromRgb(0x9A, 0xD4, 0xFF), WaterColor) { GradientOrigin = new Point(0.35, 0.35) });

    public static readonly DependencyProperty FromProperty = DependencyProperty.Register(
        nameof(From), typeof(double), typeof(WaterFillMeter), new PropertyMetadata(0d, OnInputChanged));

    public static readonly DependencyProperty ToProperty = DependencyProperty.Register(
        nameof(To), typeof(double), typeof(WaterFillMeter), new PropertyMetadata(0d, OnInputChanged));

    public static readonly DependencyProperty CelebrateProperty = DependencyProperty.Register(
        nameof(Celebrate), typeof(bool), typeof(WaterFillMeter), new PropertyMetadata(false, OnInputChanged));

    /// <summary>Nível desenhado (0 = vazia, 1 = cheia), o valor que a animação percorre.</summary>
    private static readonly DependencyProperty LevelProperty = DependencyProperty.Register(
        "Level", typeof(double), typeof(WaterFillMeter), new PropertyMetadata(0d, OnLevelChanged));

    private readonly Random _random = new();
    private readonly Viewbox _drop;
    private readonly ScaleTransform _pulse = new(1, 1);
    private readonly TranslateTransform _levelShift = new();
    private readonly TranslateTransform _frontWaveShift = new();
    private readonly TranslateTransform _backWaveShift = new();
    private readonly Canvas _effects = new() { IsHitTestVisible = false, ClipToBounds = false };
    private readonly DropShadowEffect _glow = new() { Color = WaterColor, ShadowDepth = 0, Opacity = 0 };
    private AnimationClock? _frontClock;
    private AnimationClock? _backClock;
    private bool _restartPending;

    public WaterFillMeter()
    {
        Focusable = false;
        IsTabStop = false;
        IsHitTestVisible = false;
        var icon = VectorIcons.Get(VectorIcons.Water)!;
        var outline = Geometry.Parse(icon.Outline);
        outline.Freeze();

        // A água sobe dentro de um Canvas recortado pelo contorno da gota.
        var water = new Canvas { Width = 24, Height = 24, RenderTransform = _levelShift };
        water.Children.Add(new Path { Data = WaveGeometry(-0.35), Fill = BackWaterBrush, RenderTransform = _backWaveShift });
        water.Children.Add(new Path { Data = WaveGeometry(0), Fill = WaterBrush, RenderTransform = _frontWaveShift });
        var waterHost = new Canvas { Width = 24, Height = 24, Clip = outline };
        waterHost.Children.Add(water);

        var canvas = new Canvas { Width = 24, Height = 24 };
        canvas.Children.Add(waterHost);
        canvas.Children.Add(Stroke(icon.Outline));
        canvas.Children.Add(Stroke(icon.Detail));

        _drop = new Viewbox
        {
            Child = canvas,
            Stretch = Stretch.Uniform,
            RenderTransformOrigin = new Point(0.5, 0.5),
            RenderTransform = _pulse,
        };

        var root = new Grid { ClipToBounds = false };
        root.Children.Add(_drop);
        root.Children.Add(_effects);
        Content = root;

        ApplyLevel(0);
        Loaded += (_, _) => Restart();
        Unloaded += (_, _) => StopAll();
    }

    /// <summary>Progresso antes (0 a 1), de onde a água parte.</summary>
    public double From
    {
        get => (double)GetValue(FromProperty);
        set => SetValue(FromProperty, value);
    }

    /// <summary>Progresso depois (0 a 1), até onde a água sobe.</summary>
    public double To
    {
        get => (double)GetValue(ToProperty);
        set => SetValue(ToProperty, value);
    }

    /// <summary>Meta batida: brilho azul e mais gotas.</summary>
    public bool Celebrate
    {
        get => (bool)GetValue(CelebrateProperty);
        set => SetValue(CelebrateProperty, value);
    }

    private static bool AnimationsEnabled => SystemParameters.ClientAreaAnimation;

    private static double Clamp01(double value) => double.IsNaN(value) ? 0 : Math.Clamp(value, 0, 1);

    /// <summary>
    /// Topo da água na grade 24×24. Em 0 a crista inteira fica abaixo da base (gota vazia); em 1 chega perto da ponta.
    /// </summary>
    private static double LevelY(double level) =>
        BottomLevel + WaveAmplitude - Clamp01(level) * (BottomLevel + WaveAmplitude - TopLevel);

    private static void OnInputChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var meter = (WaterFillMeter)d;
        if (!meter.IsLoaded)
        {
            meter.ShowFinal();
            return;
        }

        // Várias propriedades mudam juntas quando o DataContext troca: reinicia uma vez só.
        if (meter._restartPending)
        {
            return;
        }

        meter._restartPending = true;
        meter.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () =>
        {
            meter._restartPending = false;
            if (meter.IsLoaded)
            {
                meter.Restart();
            }
        });
    }

    private static void OnLevelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((WaterFillMeter)d).ApplyLevel((double)e.NewValue);

    private void ApplyLevel(double level) => _levelShift.Y = LevelY(level);

    /// <summary>Estado final sem movimento: nível em To e, se for o caso, brilho fixo.</summary>
    private void ShowFinal()
    {
        StopAll();
        SetGlow(Celebrate ? GlowOpacity : 0);
    }

    private void Restart()
    {
        if (!AnimationsEnabled)
        {
            ShowFinal();
            return;
        }

        StopAll();
        StartWaves();

        var level = new DoubleAnimation(Clamp01(From), Clamp01(To), LevelDuration)
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
        };
        level.Completed += (_, _) =>
        {
            if (IsLoaded)
            {
                Pulse();
                Splash(Celebrate ? _random.Next(10, 15) : _random.Next(4, 7));
            }
        };
        BeginAnimation(LevelProperty, level);

        if (Celebrate)
        {
            // O brilho acende quando a água chega ao topo e assenta num valor fixo.
            var glow = new DoubleAnimationUsingKeyFrames { Duration = TimeSpan.FromMilliseconds(1300) };
            glow.KeyFrames.Add(new DiscreteDoubleKeyFrame(0, KeyTime.FromPercent(0)));
            glow.KeyFrames.Add(new LinearDoubleKeyFrame(0.2, KeyTime.FromPercent(0.5)));
            glow.KeyFrames.Add(new EasingDoubleKeyFrame(1, KeyTime.FromPercent(0.75), new QuadraticEase { EasingMode = EasingMode.EaseOut }));
            glow.KeyFrames.Add(new EasingDoubleKeyFrame(GlowOpacity, KeyTime.FromPercent(1), new QuadraticEase { EasingMode = EasingMode.EaseInOut }));
            SetGlow(GlowOpacity);
            _glow.BeginAnimation(DropShadowEffect.OpacityProperty, glow);
        }
    }

    /// <summary>Para ondas, nível, pulso, brilho e gotas, deixando o nível em To.</summary>
    private void StopAll()
    {
        StopWaves();
        BeginAnimation(LevelProperty, null);
        SetCurrentValue(LevelProperty, Clamp01(To));
        _pulse.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        _pulse.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        _glow.BeginAnimation(DropShadowEffect.OpacityProperty, null);
        SetGlow(0);
        ClearDrops();
    }

    private void SetGlow(double opacity)
    {
        _glow.Opacity = opacity;
        _drop.Effect = opacity > 0 ? _glow : null;
    }

    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo);
        _glow.BlurRadius = Math.Max(6, Math.Min(ActualWidth, ActualHeight) * 0.4);
    }

    // Ondas: cada camada desliza exatamente um período em loop, em sentidos opostos.
    private void StartWaves()
    {
        _frontClock = StartWave(_frontWaveShift, -WavePeriod, TimeSpan.FromSeconds(1.4));
        _backClock = StartWave(_backWaveShift, WavePeriod, TimeSpan.FromSeconds(2.1));
    }

    private static AnimationClock StartWave(TranslateTransform shift, double to, TimeSpan duration)
    {
        var clock = new DoubleAnimation(0, to, duration) { RepeatBehavior = RepeatBehavior.Forever }.CreateClock();
        shift.ApplyAnimationClock(TranslateTransform.XProperty, clock);
        return clock;
    }

    private void StopWaves()
    {
        _frontClock?.Controller?.Stop();
        _backClock?.Controller?.Stop();
        _frontWaveShift.ApplyAnimationClock(TranslateTransform.XProperty, null);
        _backWaveShift.ApplyAnimationClock(TranslateTransform.XProperty, null);
        _frontClock = null;
        _backClock = null;
    }

    /// <summary>Pulso da gota: 1 → 1,12 → 1.</summary>
    private void Pulse()
    {
        var animation = new DoubleAnimationUsingKeyFrames { Duration = PulseDuration };
        animation.KeyFrames.Add(new EasingDoubleKeyFrame(1.12, KeyTime.FromPercent(0.4), new QuadraticEase { EasingMode = EasingMode.EaseOut }));
        animation.KeyFrames.Add(new EasingDoubleKeyFrame(1, KeyTime.FromPercent(1), new QuadraticEase { EasingMode = EasingMode.EaseIn }));
        _pulse.BeginAnimation(ScaleTransform.ScaleXProperty, animation);
        _pulse.BeginAnimation(ScaleTransform.ScaleYProperty, animation);
    }

    /// <summary>Gotinhas que sobem da superfície e somem; cada uma sai do Canvas ao terminar.</summary>
    private void Splash(int count)
    {
        var size = Math.Min(ActualWidth, ActualHeight);
        if (size <= 0)
        {
            return;
        }

        // Converte o topo da água da grade 24×24 para pixels do controle (Viewbox uniforme, centralizado).
        var scale = size / 24;
        var left = (ActualWidth - size) / 2;
        var top = (ActualHeight - size) / 2;
        var surface = top + Math.Max(TopLevel, LevelY(To)) * scale;
        var spread = size * 0.4;
        var celebrate = Celebrate;

        for (var i = 0; i < count; i++)
        {
            var dropSize = Math.Max(2, size * (0.07 + _random.NextDouble() * 0.06));
            var drop = new Ellipse { Width = dropSize, Height = dropSize, Fill = DropBrush, Opacity = 0, IsHitTestVisible = false };
            Canvas.SetLeft(drop, left + size * (0.35 + _random.NextDouble() * 0.3) - dropSize / 2);
            Canvas.SetTop(drop, surface - dropSize / 2);
            var move = new TranslateTransform();
            drop.RenderTransform = move;
            _effects.Children.Add(drop);

            var begin = TimeSpan.FromMilliseconds(_random.NextDouble() * (celebrate ? 350 : 120));
            var duration = DropDuration + TimeSpan.FromMilliseconds(_random.Next(-80, 120));
            var rise = size * (0.45 + _random.NextDouble() * (celebrate ? 0.6 : 0.35));

            var x = new DoubleAnimation(0, (_random.NextDouble() - 0.5) * 2 * spread, duration) { BeginTime = begin };
            var y = new DoubleAnimationUsingKeyFrames { BeginTime = begin, Duration = duration };
            y.KeyFrames.Add(new EasingDoubleKeyFrame(-rise, KeyTime.FromPercent(0.5), new QuadraticEase { EasingMode = EasingMode.EaseOut }));
            y.KeyFrames.Add(new EasingDoubleKeyFrame(-rise * 0.55, KeyTime.FromPercent(1), new QuadraticEase { EasingMode = EasingMode.EaseIn }));
            var fade = new DoubleAnimationUsingKeyFrames { BeginTime = begin, Duration = duration };
            fade.KeyFrames.Add(new LinearDoubleKeyFrame(1, KeyTime.FromPercent(0.1)));
            fade.KeyFrames.Add(new LinearDoubleKeyFrame(1, KeyTime.FromPercent(0.5)));
            fade.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromPercent(1)));
            fade.Completed += (_, _) => RemoveDrop(drop);

            move.BeginAnimation(TranslateTransform.XProperty, x);
            move.BeginAnimation(TranslateTransform.YProperty, y);
            drop.BeginAnimation(OpacityProperty, fade);
        }
    }

    private void RemoveDrop(Ellipse drop)
    {
        drop.BeginAnimation(OpacityProperty, null);
        if (drop.RenderTransform is TranslateTransform move)
        {
            move.BeginAnimation(TranslateTransform.XProperty, null);
            move.BeginAnimation(TranslateTransform.YProperty, null);
        }

        _effects.Children.Remove(drop);
    }

    private void ClearDrops()
    {
        foreach (var drop in _effects.Children.OfType<Ellipse>().ToList())
        {
            RemoveDrop(drop);
        }

        _effects.Children.Clear();
    }

    /// <summary>Traço da gota no Foreground herdado, igual ao template implícito de <see cref="VectorIcon"/>.</summary>
    private Path Stroke(string data)
    {
        var path = new Path
        {
            Data = Geometry.Parse(data),
            StrokeThickness = 1.6,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            StrokeLineJoin = PenLineJoin.Round,
        };
        path.SetBinding(Shape.StrokeProperty, new Binding(nameof(Foreground)) { Source = this });
        return path;
    }

    /// <summary>
    /// Superfície em senoide com a linha média em y = <paramref name="offset"/> e corpo até bem abaixo da base;
    /// vai de x −12 a 36 para cobrir a gota enquanto desliza um período para qualquer lado.
    /// </summary>
    private static Geometry WaveGeometry(double offset)
    {
        var points = new PointCollection();
        for (var x = -12.0; x <= 36.0001; x += 0.5)
        {
            points.Add(new Point(x, offset + WaveAmplitude * Math.Sin(x / WavePeriod * 2 * Math.PI)));
        }

        points.Add(new Point(36, 30));
        points.Add(new Point(-12, 30));
        var figure = new PathFigure(new Point(-12, offset), [new PolyLineSegment(points, false)], closed: true);
        var geometry = new PathGeometry([figure]);
        geometry.Freeze();
        return geometry;
    }

    private static Brush Frozen(Brush brush)
    {
        brush.Freeze();
        return brush;
    }
}
