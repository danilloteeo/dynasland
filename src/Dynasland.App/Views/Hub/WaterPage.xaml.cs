using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using Dynasland.App.ViewModels.Hub;

namespace Dynasland.App.Views.Hub;

/// <summary>
/// Garrafa animada da página Água. As animações reagem às mudanças de <see cref="WaterPageViewModel.Progress"/> e
/// <see cref="WaterPageViewModel.Glasses"/> (não ao clique), então disparam venha o copo da página, da ilha ou do atalho.
/// </summary>
public partial class WaterPage : UserControl
{
    // Coordenadas do Canvas da garrafa (160 x 300), as mesmas da geometria em WaterPage.xaml.
    private const double BottomLevel = 290;  // fundo interno
    private const double GoalLevel = 110;    // linha da meta (Progress = 1)
    private const double HiddenLevel = 306;  // Progress = 0: a camada inteira (cristas incluídas) fica abaixo do fundo
    private const double WavePeriod = 80;    // período da senoide em WaveShape
    private const double DropAreaLeft = 30;
    private const double DropAreaWidth = 100;

    private static readonly TimeSpan LevelDuration = TimeSpan.FromMilliseconds(700);
    private static readonly TimeSpan DropDuration = TimeSpan.FromMilliseconds(800);
    private static readonly Color WaterColor = Color.FromRgb(0x3A, 0xA7, 0xFF);
    private static readonly Brush DropBrush = CreateDropBrush();
    private const double GlowOpacity = 0.75;

    /// <summary>Intensidade da agitação das ondas (0 = normal, 1 = máxima), animada após cada copo.</summary>
    private static readonly DependencyProperty AgitationProperty = DependencyProperty.Register(
        "Agitation", typeof(double), typeof(WaterPage), new PropertyMetadata(0d, OnAgitationChanged));

    private readonly Random _random = new();
    private WaterPageViewModel? _viewModel;
    private AnimationClock? _frontClock;
    private AnimationClock? _backClock;
    private DropShadowEffect? _glow;
    private bool _goalReached;
    private int _lastGlasses;

    public WaterPage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        DataContextChanged += OnDataContextChanged;
    }

    private static bool AnimationsEnabled => SystemParameters.ClientAreaAnimation;

    /// <summary>Progress 0 esconde a água; de 0 a 1 o nível vai linearmente do fundo (y 290) à linha da meta (y 110).</summary>
    private static double LevelFor(double progress) =>
        progress <= 0 ? HiddenLevel : BottomLevel - Math.Min(1, progress) * (BottomLevel - GoalLevel);

    private void OnLoaded(object sender, RoutedEventArgs e) => Attach(DataContext as WaterPageViewModel);

    private void OnUnloaded(object sender, RoutedEventArgs e) => Detach();

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (IsLoaded)
        {
            Attach(e.NewValue as WaterPageViewModel);
        }
    }

    private void Attach(WaterPageViewModel? viewModel)
    {
        Detach();
        _viewModel = viewModel;
        if (viewModel is not null)
        {
            viewModel.PropertyChanged += OnViewModelPropertyChanged;
        }

        // Ao abrir a aba o nível vai direto ao valor atual, sem animar a partir do zero.
        SetLevel(viewModel?.Progress ?? 0, animate: false);
        _lastGlasses = viewModel?.Glasses ?? 0;
        SetGoalGlow(viewModel is not null && GoalReached(viewModel), animate: false, pulse: false);

        if (AnimationsEnabled)
        {
            StartWaves();
        }
    }

    /// <summary>Para tudo o que roda (ondas, agitação, nível, pulso, brilho, gotas) e solta o ViewModel.</summary>
    private void Detach()
    {
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            _viewModel = null;
        }

        StopWaves();
        BeginAnimation(AgitationProperty, null);
        var level = LevelTransform.Y;
        LevelTransform.BeginAnimation(TranslateTransform.YProperty, null);
        LevelTransform.Y = level;
        CounterScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        CounterScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        RemoveGlow();
        ClearDrops();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not WaterPageViewModel viewModel || viewModel != _viewModel)
        {
            return;
        }

        // Na primeira carga (HasLoaded ainda falso) tudo é aplicado direto, sem animação.
        var animate = viewModel.HasLoaded && AnimationsEnabled;
        switch (e.PropertyName)
        {
            case nameof(WaterPageViewModel.Progress):
                SetLevel(viewModel.Progress, animate);
                break;

            case nameof(WaterPageViewModel.Glasses):
                var increased = viewModel.Glasses > _lastGlasses;
                _lastGlasses = viewModel.Glasses;
                var reached = GoalReached(viewModel);
                if (animate && increased)
                {
                    Agitate();
                    Splash(_random.Next(8, 13), minRise: 28, maxRise: 60, maxDelayMs: 120, spread: 36);
                    Pulse();
                    if (reached)
                    {
                        Splash(_random.Next(12, 17), minRise: 70, maxRise: 120, maxDelayMs: 450, spread: 70);
                    }
                }

                SetGoalGlow(reached, animate, pulse: animate && increased && reached);
                break;

            case nameof(WaterPageViewModel.Goal):
                SetGoalGlow(GoalReached(viewModel), animate, pulse: false);
                break;
        }
    }

    private static bool GoalReached(WaterPageViewModel viewModel) => viewModel.Goal > 0 && viewModel.Glasses >= viewModel.Goal;

    private void SetLevel(double progress, bool animate)
    {
        var target = LevelFor(progress);
        if (!animate)
        {
            LevelTransform.BeginAnimation(TranslateTransform.YProperty, null);
            LevelTransform.Y = target;
            return;
        }

        // From explícito = valor atual (inclusive no meio de outra animação), para não saltar.
        var animation = new DoubleAnimation(LevelTransform.Y, target, LevelDuration)
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
        };
        LevelTransform.BeginAnimation(TranslateTransform.YProperty, animation);
        LevelTransform.Y = target;
    }

    // Ondas: cada camada desliza exatamente um período em loop; sentidos e velocidades diferentes.
    private void StartWaves()
    {
        StopWaves();
        _frontClock = StartWave(FrontWaveShift, -WavePeriod, TimeSpan.FromSeconds(2.4));
        _backClock = StartWave(BackWaveShift, WavePeriod, TimeSpan.FromSeconds(3.6));
    }

    private static AnimationClock StartWave(TranslateTransform shift, double to, TimeSpan duration)
    {
        var animation = new DoubleAnimation(0, to, duration) { RepeatBehavior = RepeatBehavior.Forever };
        var clock = animation.CreateClock();
        shift.ApplyAnimationClock(TranslateTransform.XProperty, clock);
        return clock;
    }

    private void StopWaves()
    {
        _frontClock?.Controller?.Stop();
        _backClock?.Controller?.Stop();
        FrontWaveShift.ApplyAnimationClock(TranslateTransform.XProperty, null);
        BackWaveShift.ApplyAnimationClock(TranslateTransform.XProperty, null);
        _frontClock = null;
        _backClock = null;
    }

    /// <summary>Agita as ondas por ~1 s: sobe a intensidade rápido e volta ao normal.</summary>
    private void Agitate()
    {
        var animation = new DoubleAnimationUsingKeyFrames
        {
            Duration = TimeSpan.FromMilliseconds(1000),
            FillBehavior = FillBehavior.Stop,
        };
        animation.KeyFrames.Add(new EasingDoubleKeyFrame(1, KeyTime.FromPercent(0.15), new QuadraticEase { EasingMode = EasingMode.EaseOut }));
        animation.KeyFrames.Add(new EasingDoubleKeyFrame(0, KeyTime.FromPercent(1), new QuadraticEase { EasingMode = EasingMode.EaseInOut }));
        BeginAnimation(AgitationProperty, animation);
    }

    private static void OnAgitationChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((WaterPage)d).ApplyAgitation((double)e.NewValue);

    private void ApplyAgitation(double agitation)
    {
        FrontWaveScale.ScaleY = 1 + 1.2 * agitation;
        BackWaveScale.ScaleY = 1 + 0.9 * agitation;
        SetSpeed(_frontClock, 1 + 2.0 * agitation);
        SetSpeed(_backClock, 1 + 1.5 * agitation);
    }

    private static void SetSpeed(AnimationClock? clock, double ratio)
    {
        if (clock?.Controller is { } controller)
        {
            controller.SpeedRatio = ratio;
        }
    }

    /// <summary>Pulso do contador: 1 → 1,15 → 1.</summary>
    private void Pulse()
    {
        var animation = new DoubleAnimationUsingKeyFrames { Duration = TimeSpan.FromMilliseconds(360) };
        animation.KeyFrames.Add(new EasingDoubleKeyFrame(1.15, KeyTime.FromPercent(0.35), new QuadraticEase { EasingMode = EasingMode.EaseOut }));
        animation.KeyFrames.Add(new EasingDoubleKeyFrame(1, KeyTime.FromPercent(1), new QuadraticEase { EasingMode = EasingMode.EaseIn }));
        CounterScale.BeginAnimation(ScaleTransform.ScaleXProperty, animation);
        CounterScale.BeginAnimation(ScaleTransform.ScaleYProperty, animation);
    }

    /// <summary>Gotas que sobem do topo da água em arco e somem; cada uma é removida do Canvas ao terminar.</summary>
    private void Splash(int count, double minRise, double maxRise, int maxDelayMs, double spread)
    {
        var surface = Math.Min(BottomLevel, LevelFor(_viewModel?.Progress ?? 0));
        for (var i = 0; i < count; i++)
        {
            var size = 4 + _random.NextDouble() * 4;
            var drop = new Ellipse { Width = size, Height = size, Fill = DropBrush, Opacity = 0, IsHitTestVisible = false };
            Canvas.SetLeft(drop, DropAreaLeft + _random.NextDouble() * DropAreaWidth - size / 2);
            Canvas.SetTop(drop, surface - size / 2);
            var move = new TranslateTransform();
            drop.RenderTransform = move;
            EffectsCanvas.Children.Add(drop);

            var begin = TimeSpan.FromMilliseconds(_random.NextDouble() * maxDelayMs);
            var duration = DropDuration + TimeSpan.FromMilliseconds(_random.Next(-80, 120));
            var rise = minRise + _random.NextDouble() * (maxRise - minRise);

            var x = new DoubleAnimation(0, (_random.NextDouble() - 0.5) * 2 * spread, duration) { BeginTime = begin };

            var y = new DoubleAnimationUsingKeyFrames { BeginTime = begin, Duration = duration };
            y.KeyFrames.Add(new EasingDoubleKeyFrame(-rise, KeyTime.FromPercent(0.45), new QuadraticEase { EasingMode = EasingMode.EaseOut }));
            y.KeyFrames.Add(new EasingDoubleKeyFrame(-rise * 0.45, KeyTime.FromPercent(1), new QuadraticEase { EasingMode = EasingMode.EaseIn }));

            var fade = new DoubleAnimationUsingKeyFrames { BeginTime = begin, Duration = duration };
            fade.KeyFrames.Add(new LinearDoubleKeyFrame(1, KeyTime.FromPercent(0.08)));
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

        EffectsCanvas.Children.Remove(drop);
    }

    private void ClearDrops()
    {
        foreach (var drop in EffectsCanvas.Children.OfType<Ellipse>().ToList())
        {
            RemoveDrop(drop);
        }

        EffectsCanvas.Children.Clear();
    }

    /// <summary>Brilho azul no contorno enquanto a meta está batida; some ao cair abaixo dela.</summary>
    private void SetGoalGlow(bool reached, bool animate, bool pulse)
    {
        var wasReached = _goalReached;
        _goalReached = reached;
        if (reached)
        {
            if (_glow is null)
            {
                _glow = new DropShadowEffect { Color = WaterColor, ShadowDepth = 0, BlurRadius = 26, Opacity = animate ? 0 : GlowOpacity };
                BottleOutline.Effect = _glow;
            }

            if (animate && (pulse || !wasReached))
            {
                var animation = new DoubleAnimationUsingKeyFrames { Duration = TimeSpan.FromMilliseconds(1100) };
                // Primeiro quadro explícito: parte do brilho atual (0 quando acabou de aparecer), não do valor base.
                animation.KeyFrames.Add(new DiscreteDoubleKeyFrame(_glow.Opacity, KeyTime.FromPercent(0)));
                animation.KeyFrames.Add(new EasingDoubleKeyFrame(1, KeyTime.FromPercent(0.25), new QuadraticEase { EasingMode = EasingMode.EaseOut }));
                animation.KeyFrames.Add(new EasingDoubleKeyFrame(GlowOpacity, KeyTime.FromPercent(1), new QuadraticEase { EasingMode = EasingMode.EaseInOut }));
                _glow.BeginAnimation(DropShadowEffect.OpacityProperty, animation);
                _glow.Opacity = GlowOpacity;
            }
            else if (!animate)
            {
                _glow.BeginAnimation(DropShadowEffect.OpacityProperty, null);
                _glow.Opacity = GlowOpacity;
            }

            return;
        }

        if (_glow is null)
        {
            return;
        }

        if (!animate)
        {
            RemoveGlow();
            return;
        }

        var glow = _glow;
        var fade = new DoubleAnimation(glow.Opacity, 0, TimeSpan.FromMilliseconds(400));
        fade.Completed += (_, _) =>
        {
            if (!_goalReached && _glow == glow)
            {
                RemoveGlow();
            }
        };
        glow.BeginAnimation(DropShadowEffect.OpacityProperty, fade);
        glow.Opacity = 0;
    }

    private void RemoveGlow()
    {
        if (_glow is not null)
        {
            _glow.BeginAnimation(DropShadowEffect.OpacityProperty, null);
            _glow = null;
        }

        BottleOutline.Effect = null;
    }

    private static Brush CreateDropBrush()
    {
        var brush = new RadialGradientBrush(Color.FromRgb(0x9A, 0xD4, 0xFF), WaterColor)
        {
            GradientOrigin = new Point(0.35, 0.35),
        };
        brush.Freeze();
        return brush;
    }
}
