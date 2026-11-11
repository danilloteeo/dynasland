using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shapes;

namespace Dynasland.Modules.Water;

/// <summary>
/// Silhueta humana de frente que se enche de água dos pés (<see cref="Progress"/> 0) ao topo da cabeça (1):
/// ondas na superfície e, ao subir, onda agitada, gotinhas e um pulso; com <see cref="Celebrate"/>, brilho
/// azul e mais gotas. O contorno usa o Foreground herdado, como os ícones vetoriais.
/// </summary>
/// <remarks>
/// Ao carregar, o nível vai direto ao valor atual (abrir o painel não anima a partir do zero); mudanças de
/// <see cref="Progress"/> com o controle carregado animam (descer, ao desfazer, sem gotas nem pulso). Ondas
/// só correm enquanto carregado, e nada anima com <see cref="SystemParameters.ClientAreaAnimation"/> desligado.
/// O traço fica com espessura constante em pixels: o contorno é escalado pela geometria, não por Viewbox.
/// </remarks>
public sealed class WaterBodyMeter : UserControl
{
    // Grade 100 × 200: cabeça no topo (y 3,5), planta dos pés na base (y 197), simétrica em x = 50.
    private const double GridWidth = 100;
    private const double GridHeight = 200;
    private const double FeetY = 197;
    private const double HeadTopY = 3.5;
    private const double WaveAmplitude = 2.2;
    private const double WavePeriod = 28;
    private const double BackWaveOffset = -1.6;
    private const double GlowOpacity = 0.8;

    /// <summary>
    /// Contorno único: cabeça (arco), pescoço, ombros, braços afastados do tronco, tronco, pernas separadas e pés.
    /// Gerado a partir da metade direita espelhada.
    /// </summary>
    private const string Outline =
        "M55.6,29.28 C55.6,33.2 55.7,35.3 56.4,36.2 C60,38 66,38.4 71.5,40.2 C77.2,42 80,46.5 80.2,52.5 " +
        "C81.6,62 83.6,72 85.4,81 C87,90 88.8,98 89.6,104.5 C91,109.5 90.8,117 86.8,117.8 C83,118.4 82,113.6 82,108 " +
        "C81.2,98.5 78.8,89.5 77,80.5 C75.4,70.5 73,61.5 69.8,56.5 C68.8,64 66.6,73 65.6,82 C65,88 66.6,93 66.8,100 " +
        "C67.8,116 65.6,133 64.4,146 C63.8,153 66.2,161 64.8,170 C64.2,177 63,182 62.6,185 C66,187.5 70,190.5 70,193.8 " +
        "C70,196.6 66,197 60,197 C57.8,197 55.6,197 55,196 C54.2,194.5 54.6,188 55,183 C55.4,177 54.4,163 55.2,155 " +
        "C55.6,150 55.4,145 55,140 C54.4,128 53.2,116 52,107 C51.4,105.2 50.6,104.8 50,104.8 " +
        "C49.4,104.8 48.6,105.2 48,107 C46.8,116 45.6,128 45,140 C44.6,145 44.4,150 44.8,155 C45.6,163 44.6,177 45,183 " +
        "C45.4,188 45.8,194.5 45,196 C44.4,197 42.2,197 40,197 C34,197 30,196.6 30,193.8 C30,190.5 34,187.5 37.4,185 " +
        "C37,182 35.8,177 35.2,170 C33.8,161 36.2,153 35.6,146 C34.4,133 32.2,116 33.2,100 C33.4,93 35,88 34.4,82 " +
        "C33.4,73 31.2,64 30.2,56.5 C27,61.5 24.6,70.5 23,80.5 C21.2,89.5 18.8,98.5 18,108 C18,113.6 17,118.4 13.2,117.8 " +
        "C9.2,117 9,109.5 10.4,104.5 C11.2,98 13,90 14.6,81 C16.4,72 18.4,62 19.8,52.5 C20,46.5 22.8,42 28.5,40.2 " +
        "C34,38.4 40,38 43.6,36.2 C44.3,35.3 44.4,33.2 44.4,29.28 A13.5,13.5 0 1 1 55.6,29.28 Z";

    private static readonly TimeSpan LevelDuration = TimeSpan.FromMilliseconds(900);
    private static readonly TimeSpan PulseDuration = TimeSpan.FromMilliseconds(380);
    private static readonly Geometry BodyGeometry = FrozenGeometry(Outline);

    public static readonly DependencyProperty ProgressProperty = DependencyProperty.Register(
        nameof(Progress), typeof(double), typeof(WaterBodyMeter), new PropertyMetadata(0d, OnProgressChanged));

    public static readonly DependencyProperty CelebrateProperty = DependencyProperty.Register(
        nameof(Celebrate), typeof(bool), typeof(WaterBodyMeter), new PropertyMetadata(false, OnCelebrateChanged));

    /// <summary>Nível desenhado (0 = pés, 1 = topo da cabeça), o valor que a animação percorre.</summary>
    private static readonly DependencyProperty LevelProperty = DependencyProperty.Register(
        "Level", typeof(double), typeof(WaterBodyMeter), new PropertyMetadata(0d, OnLevelChanged));

    private readonly Random _random = new();
    private readonly Grid _figure;
    private readonly Path _stroke;
    private readonly MatrixTransform _layout = new();
    private readonly ScaleTransform _pulse = new(1, 1);
    private readonly TranslateTransform _levelShift = new();
    private readonly TranslateTransform _frontWaveShift = new();
    private readonly TranslateTransform _backWaveShift = new();
    private readonly ScaleTransform _frontWaveSwell = new(1, 1);
    private readonly ScaleTransform _backWaveSwell = new(1, 1);
    private readonly Canvas _effects = new() { IsHitTestVisible = false, ClipToBounds = false };
    private readonly DropShadowEffect _glow = new() { Color = WaterFx.WaterColor, ShadowDepth = 0, Opacity = 0 };
    private readonly Geometry _strokeGeometry;
    private AnimationClock? _frontClock;
    private AnimationClock? _backClock;
    private double _scale = 1;
    private Point _origin;

    public WaterBodyMeter()
    {
        Focusable = false;
        IsTabStop = false;
        IsHitTestVisible = false;

        // A água sobe num Canvas na grade do corpo, recortado pelo contorno e escalado para o tamanho do controle.
        var water = new Canvas { Width = GridWidth, Height = GridHeight, RenderTransform = _levelShift };
        water.Children.Add(Wave(BackWaveOffset, WaterFx.BackWaterBrush, _backWaveShift, _backWaveSwell));
        water.Children.Add(Wave(0, WaterFx.WaterBrush, _frontWaveShift, _frontWaveSwell));
        var waterHost = new Canvas { Width = GridWidth, Height = GridHeight, Clip = BodyGeometry, RenderTransform = _layout };
        waterHost.Children.Add(water);

        // O traço usa a geometria transformada (não RenderTransform) para manter a espessura em pixels.
        _strokeGeometry = BodyGeometry.Clone();
        _strokeGeometry.Transform = _layout;
        _stroke = new Path
        {
            Data = _strokeGeometry,
            StrokeThickness = 1.8,
            StrokeLineJoin = PenLineJoin.Round,
        };
        _stroke.SetBinding(Shape.StrokeProperty, new Binding(nameof(Foreground)) { Source = this });

        var canvas = new Canvas();
        canvas.Children.Add(waterHost);
        canvas.Children.Add(_stroke);
        _figure = new Grid
        {
            RenderTransformOrigin = new Point(0.5, 0.5),
            RenderTransform = _pulse,
        };
        _figure.Children.Add(canvas);

        var root = new Grid { ClipToBounds = false };
        root.Children.Add(_figure);
        root.Children.Add(_effects);
        Content = root;

        ApplyLevel(0);
        Loaded += (_, _) => OnLoaded();
        Unloaded += (_, _) => StopAll();
    }

    /// <summary>Copos de hoje sobre a meta (0 a 1).</summary>
    public double Progress
    {
        get => (double)GetValue(ProgressProperty);
        set => SetValue(ProgressProperty, value);
    }

    /// <summary>Meta batida: brilho azul e mais gotas.</summary>
    public bool Celebrate
    {
        get => (bool)GetValue(CelebrateProperty);
        set => SetValue(CelebrateProperty, value);
    }

    private double Target => WaterFx.Clamp01(Progress);

    /// <summary>
    /// Linha média da onda na grade. Em 0 a crista fica abaixo da sola dos pés; em 1 até a onda de trás passa
    /// acima do topo da cabeça (silhueta totalmente cheia).
    /// </summary>
    private static double LevelY(double level)
    {
        var bottom = FeetY + WaveAmplitude + 0.5;
        var top = HeadTopY + BackWaveOffset - WaveAmplitude - 1;
        return bottom - WaterFx.Clamp01(level) * (bottom - top);
    }

    private static void OnProgressChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var meter = (WaterBodyMeter)d;
        var from = WaterFx.Clamp01((double)e.OldValue);
        var to = WaterFx.Clamp01((double)e.NewValue);
        if (!meter.IsLoaded || !WaterFx.AnimationsEnabled)
        {
            meter.ShowLevel(to);
            return;
        }

        meter.AnimateLevel(to, rising: to > from + 0.0001);
    }

    private static void OnCelebrateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var meter = (WaterBodyMeter)d;
        var celebrate = (bool)e.NewValue;
        if (!celebrate)
        {
            meter._glow.BeginAnimation(DropShadowEffect.OpacityProperty, null);
            meter.SetGlow(0);
        }
        else if (!meter.IsLoaded || !WaterFx.AnimationsEnabled)
        {
            meter.SetGlow(GlowOpacity);
        }
        else
        {
            // O brilho acende quando a água chega ao topo e assenta num valor fixo.
            var glow = new DoubleAnimationUsingKeyFrames { Duration = TimeSpan.FromMilliseconds(1300) };
            glow.KeyFrames.Add(new DiscreteDoubleKeyFrame(0, KeyTime.FromPercent(0)));
            glow.KeyFrames.Add(new LinearDoubleKeyFrame(0.2, KeyTime.FromPercent(0.5)));
            glow.KeyFrames.Add(new EasingDoubleKeyFrame(1, KeyTime.FromPercent(0.75), new QuadraticEase { EasingMode = EasingMode.EaseOut }));
            glow.KeyFrames.Add(new EasingDoubleKeyFrame(GlowOpacity, KeyTime.FromPercent(1), new QuadraticEase { EasingMode = EasingMode.EaseInOut }));
            meter.SetGlow(GlowOpacity);
            meter._glow.BeginAnimation(DropShadowEffect.OpacityProperty, glow);
        }
    }

    private static void OnLevelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((WaterBodyMeter)d).ApplyLevel((double)e.NewValue);

    private void ApplyLevel(double level) => _levelShift.Y = LevelY(level);

    /// <summary>Primeira exibição: nível direto no valor atual, ondas correndo e brilho fixo se a meta já foi batida.</summary>
    private void OnLoaded()
    {
        StopAll();
        SetGlow(Celebrate ? GlowOpacity : 0);
        if (WaterFx.AnimationsEnabled)
        {
            _frontClock = WaterFx.StartWave(_frontWaveShift, -WavePeriod, TimeSpan.FromSeconds(1.6));
            _backClock = WaterFx.StartWave(_backWaveShift, WavePeriod, TimeSpan.FromSeconds(2.4));
        }
    }

    /// <summary>Nível parado em <paramref name="level"/> (sem animação).</summary>
    private void ShowLevel(double level)
    {
        BeginAnimation(LevelProperty, null);
        SetCurrentValue(LevelProperty, level);
    }

    private void AnimateLevel(double to, bool rising)
    {
        var current = (double)GetValue(LevelProperty);
        var level = new DoubleAnimation(current, to, LevelDuration)
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
        };

        if (rising)
        {
            Swell();
            level.Completed += (_, _) =>
            {
                if (IsLoaded)
                {
                    WaterFx.Pulse(_pulse, 1.04, PulseDuration);
                    Splash(Celebrate ? _random.Next(12, 18) : _random.Next(5, 8));
                }
            };
        }

        BeginAnimation(LevelProperty, level);
    }

    /// <summary>Agita a superfície: as ondas crescem e voltam ao normal enquanto o nível sobe.</summary>
    private void Swell()
    {
        var swell = new DoubleAnimationUsingKeyFrames { Duration = TimeSpan.FromMilliseconds(1400) };
        swell.KeyFrames.Add(new EasingDoubleKeyFrame(2.4, KeyTime.FromPercent(0.18), new QuadraticEase { EasingMode = EasingMode.EaseOut }));
        swell.KeyFrames.Add(new EasingDoubleKeyFrame(1, KeyTime.FromPercent(1), new CubicEase { EasingMode = EasingMode.EaseInOut }));
        _frontWaveSwell.BeginAnimation(ScaleTransform.ScaleYProperty, swell);
        _backWaveSwell.BeginAnimation(ScaleTransform.ScaleYProperty, swell);
    }

    /// <summary>Para ondas, nível, pulso, brilho e gotas, deixando o nível no valor atual.</summary>
    private void StopAll()
    {
        WaterFx.StopWave(ref _frontClock, _frontWaveShift);
        WaterFx.StopWave(ref _backClock, _backWaveShift);
        _frontWaveSwell.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        _backWaveSwell.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        ShowLevel(Target);
        WaterFx.StopPulse(_pulse);
        _glow.BeginAnimation(DropShadowEffect.OpacityProperty, null);
        SetGlow(0);
        WaterFx.ClearDrops(_effects);
    }

    private void SetGlow(double opacity)
    {
        _glow.Opacity = opacity;
        _figure.Effect = opacity > 0 ? _glow : null;
    }

    /// <summary>Encaixa a grade 100 × 200 no controle (uniforme, centralizada), com folga para o traço.</summary>
    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo);
        _stroke.StrokeThickness = ActualHeight < 100 ? 1.3 : 1.8;
        var inset = _stroke.StrokeThickness;
        var width = Math.Max(0, ActualWidth - inset * 2);
        var height = Math.Max(0, ActualHeight - inset * 2);
        _scale = Math.Min(width / GridWidth, height / GridHeight);
        _origin = new Point((ActualWidth - GridWidth * _scale) / 2, (ActualHeight - GridHeight * _scale) / 2);
        _layout.Matrix = new Matrix(_scale, 0, 0, _scale, _origin.X, _origin.Y);
        _glow.BlurRadius = Math.Max(6, GridWidth * _scale * 0.22);
    }

    /// <summary>Gotinhas que sobem da superfície, em torno do meio do corpo, e somem.</summary>
    private void Splash(int count)
    {
        if (_scale <= 0)
        {
            return;
        }

        var surface = _origin.Y + Math.Max(HeadTopY, LevelY(Target)) * _scale;
        var width = GridWidth * _scale;
        var celebrate = Celebrate;
        for (var i = 0; i < count; i++)
        {
            var size = Math.Max(2, width * (0.035 + _random.NextDouble() * 0.035));
            var x = _origin.X + width * (0.38 + _random.NextDouble() * 0.24);
            var begin = TimeSpan.FromMilliseconds(_random.NextDouble() * (celebrate ? 350 : 120));
            var rise = width * (0.18 + _random.NextDouble() * (celebrate ? 0.32 : 0.16));
            var drift = (_random.NextDouble() - 0.5) * width * (celebrate ? 0.6 : 0.34);
            WaterFx.LaunchDrop(_effects, _random, x, surface, size, rise, drift, begin);
        }
    }

    /// <summary>Camada de onda na grade do corpo; o ScaleTransform em y 0 (a superfície) agita a crista.</summary>
    private static Path Wave(double offset, Brush fill, TranslateTransform shift, ScaleTransform swell)
    {
        var transform = new TransformGroup();
        transform.Children.Add(swell);
        transform.Children.Add(shift);
        return new Path
        {
            Data = WaterFx.WaveGeometry(offset, WaveAmplitude, WavePeriod, -GridWidth / 2 - WavePeriod, GridWidth * 1.5 + WavePeriod, GridHeight + 40, 1),
            Fill = fill,
            RenderTransform = transform,
        };
    }

    private static Geometry FrozenGeometry(string data)
    {
        var geometry = Geometry.Parse(data);
        geometry.Freeze();
        return geometry;
    }
}
