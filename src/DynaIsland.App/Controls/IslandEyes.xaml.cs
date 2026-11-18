using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace DynaIsland.App.Controls;

/// <summary>
/// O mascote da ilha: dois olhos que piscam em intervalos aleatórios e olham para os lados de vez em quando;
/// com <see cref="IsListening"/>, viram arcos fechados que balançam. Os temporizadores só rodam visível e animado.
/// </summary>
public partial class IslandEyes : UserControl
{
    public static readonly DependencyProperty IsListeningProperty = DependencyProperty.Register(
        nameof(IsListening), typeof(bool), typeof(IslandEyes), new PropertyMetadata(false, (d, _) => ((IslandEyes)d).Refresh()));

    public static readonly DependencyProperty IsAnimatedProperty = DependencyProperty.Register(
        nameof(IsAnimated), typeof(bool), typeof(IslandEyes), new PropertyMetadata(true, (d, _) => ((IslandEyes)d).Refresh()));

    private static readonly IEasingFunction Ease = new CubicEase { EasingMode = EasingMode.EaseInOut };

    private readonly DispatcherTimer _blink;
    private readonly DispatcherTimer _glance;
    private int _glanceSide = 1;

    public IslandEyes()
    {
        InitializeComponent();
        _blink = new DispatcherTimer(DispatcherPriority.Background, Dispatcher);
        _blink.Tick += (_, _) => Blink();
        _glance = new DispatcherTimer(DispatcherPriority.Background, Dispatcher);
        _glance.Tick += (_, _) => Glance();
        IsVisibleChanged += (_, _) => Refresh();
        Unloaded += (_, _) => Stop();
        Loaded += (_, _) => Refresh();
    }

    /// <summary>Música tocando: olhos fechados em arco, balançando.</summary>
    public bool IsListening
    {
        get => (bool)GetValue(IsListeningProperty);
        set => SetValue(IsListeningProperty, value);
    }

    /// <summary>Desligado (animações desligadas nas Configurações): olhos parados.</summary>
    public bool IsAnimated
    {
        get => (bool)GetValue(IsAnimatedProperty);
        set => SetValue(IsAnimatedProperty, value);
    }

    private void Refresh()
    {
        if (OpenEyes is null)
        {
            return;
        }

        OpenEyes.Visibility = IsListening ? Visibility.Collapsed : Visibility.Visible;
        ClosedEyes.Visibility = IsListening ? Visibility.Visible : Visibility.Collapsed;

        Stop();
        if (!IsAnimated || !IsVisible)
        {
            return;
        }

        if (IsListening)
        {
            Nod.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(0, -2, TimeSpan.FromMilliseconds(250))
            {
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever,
                EasingFunction = Ease,
            });
        }
        else
        {
            Schedule(_blink, 3000, 6000);
            Schedule(_glance, 7000, 12000);
        }
    }

    private void Stop()
    {
        _blink.Stop();
        _glance.Stop();
        Lids.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        Look.BeginAnimation(TranslateTransform.XProperty, null);
        Nod.BeginAnimation(TranslateTransform.YProperty, null);
    }

    private static void Schedule(DispatcherTimer timer, int minMilliseconds, int maxMilliseconds)
    {
        timer.Interval = TimeSpan.FromMilliseconds(Random.Shared.Next(minMilliseconds, maxMilliseconds + 1));
        timer.Start();
    }

    private void Blink()
    {
        Schedule(_blink, 3000, 6000);
        Lids.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(1, 0.1, TimeSpan.FromMilliseconds(70)) { AutoReverse = true });
    }

    /// <summary>Olha para um lado (alternando) por cerca de um segundo e volta ao centro.</summary>
    private void Glance()
    {
        Schedule(_glance, 7000, 12000);
        var target = 2.0 * _glanceSide;
        _glanceSide = -_glanceSide;
        var look = new DoubleAnimationUsingKeyFrames();
        look.KeyFrames.Add(new EasingDoubleKeyFrame(target, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(180)), Ease));
        look.KeyFrames.Add(new DiscreteDoubleKeyFrame(target, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(1100))));
        look.KeyFrames.Add(new EasingDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(1300)), Ease));
        Look.BeginAnimation(TranslateTransform.XProperty, look);
    }
}
