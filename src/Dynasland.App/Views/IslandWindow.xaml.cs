using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Dynasland.App.Services;
using Dynasland.App.ViewModels;
using Dynasland.Core.Activities;
using Dynasland.Core.Events;
using Dynasland.Core.Interfaces;
using Dynasland.Core.Layout;
using Dynasland.Core.Models;
using Dynasland.Core.Session;
using Dynasland.Infrastructure.Windows;

namespace Dynasland.App.Views;

public partial class IslandWindow : Window
{
    public static readonly DependencyProperty CornerSizeProperty = DependencyProperty.Register(
        nameof(CornerSize),
        typeof(double),
        typeof(IslandWindow),
        new PropertyMetadata(IslandLayout.Compact.CornerRadius));

    private const double ExpandMilliseconds = 460;
    private const double CollapseMilliseconds = 340;
    private const double FadeOutMilliseconds = 120;
    private const double SwapMilliseconds = 240;
    private const double HoverMilliseconds = 160;
    private const double HoverScale = 1.05;
    private const double SwapOffset = 6;
    private const double GhostRise = -4;
    private const double PulseMilliseconds = 600;
    private const double PulseOpacity = 0.6;
    private const double PulseScale = 1.04;

    /// <summary>Republicar o mesmo aviso dentro deste intervalo (ex.: brilho ajustado várias vezes) não pulsa de novo.</summary>
    private static readonly TimeSpan PulseRepeatWindow = TimeSpan.FromSeconds(2);

    /// <summary>Altura do rodapé (abas e configurações), somada à do conteúdo no estado interativo.</summary>
    private const double FooterHeight = 28;

    // Overshoot na expansão (sensação de mola, ajustável em Animações) e desaceleração suave no recolhimento.
    private const double BaseSpringAmplitude = 0.22;
    private static readonly IEasingFunction NoSpringEasing = Frozen(new CubicEase { EasingMode = EasingMode.EaseOut });
    private static readonly IEasingFunction CollapseEasing = Frozen(new CubicEase { EasingMode = EasingMode.EaseInOut });
    private static readonly IEasingFunction SoftEasing = Frozen(new QuadraticEase { EasingMode = EasingMode.EaseOut });

    private readonly IslandViewModel _viewModel;
    private readonly IslandPlacementService _placement;
    private readonly IIslandSettingsService _settings;
    private readonly FullscreenDetector _fullscreen;
    private readonly ISessionStateService _session;
    private readonly IslandActions _actions;
    private readonly OutsideClickWatcher _outsideClicks = new();
    private DispatcherTimer? _pendingClick;
    private bool _hiddenForGame;
    private readonly IDisposable _gamingSubscription;
    private readonly IDisposable _alertSubscription;
    private Image? _ghost;
    private string? _lastPulseId;
    private long _lastPulseTicks;
    private bool _repositionPending;
    private IEasingFunction _expandEasing = NoSpringEasing;
    private double _expandEasingIntensity = -1;

    public IslandWindow(IslandViewModel viewModel, IslandPlacementService placement, IIslandSettingsService settings, FullscreenDetector fullscreen, ISessionStateService session, IslandActions actions, IEventBus bus, ThemeService theme)
    {
        _viewModel = viewModel;
        _placement = placement;
        _settings = settings;
        _fullscreen = fullscreen;
        _session = session;
        _actions = actions;

        InitializeComponent();
        // O fundo animável vale só nesta janela (sobrepõe o recurso congelado da aplicação).
        Resources["IslandBackgroundBrush"] = theme.IslandBrush;
        DataContext = viewModel;

        ApplyLayers(viewModel.State, animate: false);
        viewModel.PropertyChanging += OnViewModelPropertyChanging;
        viewModel.PropertyChanged += OnViewModelPropertyChanged;
        settings.Changed += OnSettingsChanged;
        fullscreen.Changed += OnFullscreenChanged;
        session.Changed += OnSessionChanged;
        _gamingSubscription = bus.Subscribe<GamingModeChangedEvent>(e => Dispatcher.BeginInvoke(() =>
        {
            _hiddenForGame = e.Active && e.HideIsland;
            UpdateVisibility();
        }));
        _alertSubscription = bus.Subscribe<ActivityPostedEvent>(e =>
        {
            if (IslandAlertPulse.ShouldPulse(e.Activity))
            {
                Dispatcher.BeginInvoke(() => PlayAlertPulse(e.Activity.Id));
            }
        });
        ApplyPressFeedback();
    }

    public double CornerSize
    {
        get => (double)GetValue(CornerSizeProperty);
        set => SetValue(CornerSizeProperty, value);
    }

    /// <summary>Animações ligadas no Dynasland e na opção do Windows "Animar controles e elementos dentro das janelas".</summary>
    private bool AnimationsAllowed => _settings.Current.AnimationsEnabled && SystemParameters.ClientAreaAnimation;

    private bool ShouldAnimate => IsVisible && AnimationsAllowed;

    private double Speed => Math.Clamp(_settings.Current.AnimationSpeed, 0.25, 3);

    /// <summary>Cria o HWND, aplica estilos Win32, posiciona e exibe (se configurado) sem roubar o foco.</summary>
    public void Start()
    {
        new WindowInteropHelper(this).EnsureHandle();
        _fullscreen.Start();
        UpdateVisibility();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        var hwnd = new WindowInteropHelper(this).Handle;
        WindowInterop.ApplyOverlayStyles(hwnd);
        HwndSource.FromHwnd(hwnd)?.AddHook(WndProc);
        _placement.Place(this);
    }

    protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
    {
        base.OnDpiChanged(oldDpi, newDpi);
        RequestReposition();
    }

    protected override void OnClosed(EventArgs e)
    {
        _viewModel.PropertyChanging -= OnViewModelPropertyChanging;
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        _settings.Changed -= OnSettingsChanged;
        _fullscreen.Changed -= OnFullscreenChanged;
        _session.Changed -= OnSessionChanged;
        _outsideClicks.Dispose();
        _gamingSubscription.Dispose();
        _alertSubscription.Dispose();
        RemoveGhost();
        base.OnClosed(e);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        switch (msg)
        {
            case WindowMessages.WM_MOUSEACTIVATE:
                // Entrega o clique sem ativar a janela: o aplicativo em uso mantém o foco.
                handled = true;
                return new IntPtr(WindowMessages.MA_NOACTIVATE);

            case WindowMessages.WM_DISPLAYCHANGE:
                RequestReposition();
                break;

            case WindowMessages.WM_SETTINGCHANGE when wParam.ToInt64() == WindowMessages.SPI_SETWORKAREA:
                RequestReposition();
                break;

            case WindowMessages.WM_SETTINGCHANGE:
                // Ex.: "Animar controles e elementos dentro das janelas"; lido depois que o WPF atualiza o cache.
                Dispatcher.BeginInvoke(DispatcherPriority.Background, ApplyPressFeedback);
                break;
        }

        return IntPtr.Zero;
    }

    /// <summary>Agrupa várias notificações de mudança de tela em um único reposicionamento.</summary>
    private void RequestReposition()
    {
        if (_repositionPending)
        {
            return;
        }

        _repositionPending = true;
        Dispatcher.BeginInvoke(DispatcherPriority.Background, () =>
        {
            _repositionPending = false;
            _placement.Place(this);
        });
    }

    private void OnSettingsChanged(object? sender, IslandSettings settings)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(() => OnSettingsChanged(sender, settings));
            return;
        }

        UpdateVisibility();
        ApplyDimensions(_viewModel.Dimensions, ShouldAnimate);
        ApplyPressFeedback();
        RequestReposition();
    }

    /// <summary>A resposta ao toque dos botões (estilos da ilha) segue a mesma regra das demais animações.</summary>
    private void ApplyPressFeedback() => IslandPressFeedback.SetIsEnabled(this, AnimationsAllowed);

    private void OnFullscreenChanged(object? sender, string? monitor) => UpdateVisibility();

    /// <summary>
    /// Bloqueio/suspensão ocultam a ilha; ao voltar, os monitores podem ter mudado (dock, resolução),
    /// então a posição é recalculada.
    /// </summary>
    private void OnSessionChanged(object? sender, SessionChangedEvent e)
    {
        UpdateVisibility();
        if (!e.IsInterrupted)
        {
            RequestReposition();
        }
    }

    /// <summary>
    /// Visível se o usuário quer a ilha, a sessão está ativa (não bloqueada nem suspensa) e nenhum
    /// aplicativo em tela cheia ocupa o monitor dela (quando "Ocultar em tela cheia" está ligado).
    /// Essas ocultações temporárias não são gravadas.
    /// </summary>
    private void UpdateVisibility()
    {
        var settings = _settings.Current;
        var coveredByFullscreen = settings.HideInFullscreen
            && _fullscreen.FullscreenMonitor is { } fullscreenMonitor
            && string.Equals(fullscreenMonitor, _placement.CurrentMonitor, StringComparison.OrdinalIgnoreCase);
        ApplyVisibility(settings.IsVisible && !coveredByFullscreen && !_session.IsInterrupted && !_hiddenForGame);
    }

    private void ApplyVisibility(bool visible)
    {
        if (visible && !IsVisible)
        {
            Show();
            _placement.Place(this);
        }
        else if (!visible && IsVisible)
        {
            Hide();
        }

        _viewModel.SetWindowVisible(visible);
    }

    /// <summary>Antes de trocar o conteúdo no mesmo estado, guarda uma imagem do atual para o cross-fade.</summary>
    private void OnViewModelPropertyChanging(object? sender, PropertyChangingEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(IslandViewModel.ExpandedContent) when _viewModel.State == IslandState.Expanded:
                CaptureGhost(ExpandedLayer);
                break;

            case nameof(IslandViewModel.InteractiveContent) when _viewModel.State == IslandState.Interactive:
                CaptureGhost(InteractiveLayer);
                break;
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(IslandViewModel.State):
                ApplyLayers(_viewModel.State, ShouldAnimate);
                UpdateOutsideClickWatcher();
                break;

            case nameof(IslandViewModel.Dimensions):
                ApplyDimensions(_viewModel.Dimensions, ShouldAnimate);
                break;

            // Troca de conteúdo sem mudar de estado (outra notificação, alternância entre atividades).
            case nameof(IslandViewModel.ExpandedContent) when _viewModel.State == IslandState.Expanded:
                PlaySwap(ExpandedLayer);
                break;

            case nameof(IslandViewModel.InteractiveContent) when _viewModel.State == IslandState.Interactive:
                PlaySwap(InteractiveLayer);
                break;
        }
    }

    private void OnIslandMouseEnter(object sender, MouseEventArgs e)
    {
        _viewModel.OnPointerEntered();
        if (_viewModel.State == IslandState.Compact)
        {
            AnimateHover(true);
        }
    }

    private void OnIslandMouseLeave(object sender, MouseEventArgs e)
    {
        _viewModel.OnPointerExited();
        AnimateHover(false);
    }

    private void OnIslandMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        // Cliques em botões e sliders dos módulos não abrem/fecham a ilha.
        if (IsInsideInteractiveControl(e.OriginalSource as DependencyObject))
        {
            return;
        }

        e.Handled = true;
        var doubleClick = _settings.Current.DoubleClickAction;
        if (doubleClick == DoubleClickAction.None)
        {
            AnimateHover(false);
            _viewModel.ToggleInteractiveCommand.Execute(null);
            return;
        }

        // Com ação de duplo clique, o clique simples espera o tempo de duplo clique do Windows.
        if (_pendingClick is { IsEnabled: true })
        {
            _pendingClick.Stop();
            _actions.Run(doubleClick);
            return;
        }

        _pendingClick ??= CreatePendingClickTimer();
        _pendingClick.Start();
    }

    private DispatcherTimer CreatePendingClickTimer()
    {
        var timer = new DispatcherTimer(DispatcherPriority.Input, Dispatcher)
        {
            Interval = TimeSpan.FromMilliseconds(System.Windows.Forms.SystemInformation.DoubleClickTime),
        };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            AnimateHover(false);
            _viewModel.ToggleInteractiveCommand.Execute(null);
        };
        return timer;
    }

    /// <summary>O gancho de "clique fora" só existe com a ilha aberta e a opção ligada.</summary>
    private void UpdateOutsideClickWatcher()
    {
        if (_viewModel.State == IslandState.Interactive && _settings.Current.CollapseOnClickOutside && IsVisible)
        {
            _outsideClicks.Start(IsOverIsland, _viewModel.CollapseInteractive);
        }
        else
        {
            _outsideClicks.Stop();
        }
    }

    private bool IsOverIsland(int screenX, int screenY)
    {
        if (!IslandHost.IsVisible)
        {
            return false;
        }

        var topLeft = IslandHost.PointToScreen(new Point(0, 0));
        var bottomRight = IslandHost.PointToScreen(new Point(IslandHost.ActualWidth, IslandHost.ActualHeight));
        return screenX >= topLeft.X && screenX <= bottomRight.X && screenY >= topLeft.Y && screenY <= bottomRight.Y;
    }

    private void ApplyDimensions(IslandDimensions requested, bool animate)
    {
        var target = IslandAppearance.Adjust(requested, _viewModel.State, _settings.Current);
        var height = target.Height + (_viewModel.State == IslandState.Interactive ? FooterHeight : 0);
        if (!animate)
        {
            SetImmediate(IslandHost, WidthProperty, target.Width);
            SetImmediate(IslandHost, HeightProperty, height);
            SetImmediate(this, CornerSizeProperty, target.CornerRadius);
            return;
        }

        var growing = height >= IslandHost.Height;
        var duration = Scaled(growing ? ExpandMilliseconds : CollapseMilliseconds);
        var easing = growing ? ExpandEasing() : CollapseEasing;

        Animate(IslandHost, WidthProperty, target.Width, duration, easing);
        Animate(IslandHost, HeightProperty, height, duration, easing);
        Animate(this, CornerSizeProperty, target.CornerRadius, duration, easing);
    }

    private void ApplyLayers(IslandState state, bool animate)
    {
        var active = LayerFor(state);
        var resizeDuration = Scaled(state == IslandState.Compact ? CollapseMilliseconds : ExpandMilliseconds);

        foreach (var layer in new[] { CompactLayer, ExpandedLayer, InteractiveLayer })
        {
            var isActive = layer == active;
            layer.IsHitTestVisible = isActive;

            if (!animate)
            {
                SetImmediate(layer, OpacityProperty, isActive ? 1 : 0);
            }
            else if (isActive)
            {
                // O conteúdo aparece depois que a cápsula já ganhou espaço, evitando corte do texto.
                Animate(layer, OpacityProperty, 1, resizeDuration * 0.55, SoftEasing, beginTime: resizeDuration * 0.4);
            }
            else
            {
                Animate(layer, OpacityProperty, 0, Scaled(FadeOutMilliseconds), null);
            }
        }

        var interactive = state == IslandState.Interactive;
        FooterRow.Height = new GridLength(interactive ? FooterHeight : 0);
        Footer.IsHitTestVisible = interactive;
        if (!animate)
        {
            SetImmediate(Footer, OpacityProperty, interactive ? 1 : 0);
        }
        else if (interactive)
        {
            Animate(Footer, OpacityProperty, 1, resizeDuration * 0.55, SoftEasing, beginTime: resizeDuration * 0.4);
        }
        else
        {
            Animate(Footer, OpacityProperty, 0, Scaled(FadeOutMilliseconds), null);
        }

        // O rodapé muda a altura total mesmo quando o conteúdo mantém o mesmo tamanho.
        ApplyDimensions(_viewModel.Dimensions, animate);

        if (state != IslandState.Compact)
        {
            AnimateHover(false);
        }
    }

    private void PlaySwap(ContentPresenter layer)
    {
        if (!ShouldAnimate)
        {
            RemoveGhost();
            return;
        }

        var duration = Scaled(SwapMilliseconds);
        layer.BeginAnimation(OpacityProperty, Frozen(new DoubleAnimation(0, 1, duration) { EasingFunction = SoftEasing }));
        if (layer.RenderTransform is TranslateTransform translate)
        {
            translate.BeginAnimation(TranslateTransform.YProperty, Frozen(new DoubleAnimation(SwapOffset, 0, duration) { EasingFunction = SoftEasing }));
        }

        FadeOutGhost(duration);
    }

    /// <summary>
    /// Fotografa a camada (ainda com o conteúdo antigo) e põe a imagem logo acima dela. Um fantasma anterior,
    /// de uma troca ainda em andamento, é descartado antes.
    /// </summary>
    private void CaptureGhost(ContentPresenter layer)
    {
        RemoveGhost();
        if (!ShouldAnimate || layer.Content is null || layer.Opacity <= 0.01)
        {
            return;
        }

        var width = layer.ActualWidth;
        var height = layer.ActualHeight;
        if (width < 1 || height < 1)
        {
            return;
        }

        // A imagem já sai com a opacidade atual da camada (troca no meio de outra), então o fantasma começa em 1.
        _ghost = new Image
        {
            Source = IslandSnapshotImage.Capture(layer, width, height),
            Width = width,
            Height = height,
            Stretch = Stretch.Fill,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            IsHitTestVisible = false,
            RenderTransform = new TranslateTransform(),
        };
        ContentGrid.Children.Insert(ContentGrid.Children.IndexOf(layer) + 1, _ghost);
    }

    /// <summary>O fantasma esmaece e sobe um pouco enquanto o conteúdo novo entra por baixo dele.</summary>
    private void FadeOutGhost(TimeSpan duration)
    {
        if (_ghost is not { } ghost)
        {
            return;
        }

        // Sem congelar: o Completed é inscrito antes de a animação começar.
        var fade = new DoubleAnimation(0, duration) { EasingFunction = SoftEasing };
        fade.Completed += (_, _) =>
        {
            if (_ghost == ghost)
            {
                RemoveGhost();
            }
        };
        ghost.BeginAnimation(OpacityProperty, fade);
        ghost.RenderTransform.BeginAnimation(TranslateTransform.YProperty, Frozen(new DoubleAnimation(0, GhostRise, duration) { EasingFunction = SoftEasing }));
    }

    private void RemoveGhost()
    {
        if (_ghost is not { } ghost)
        {
            return;
        }

        _ghost = null;
        ghost.BeginAnimation(OpacityProperty, null);
        ghost.RenderTransform.BeginAnimation(TranslateTransform.YProperty, null);
        ghost.Source = null;
        ContentGrid.Children.Remove(ghost);
    }

    /// <summary>Um aviso importante chegou: o anel de acento pulsa uma vez ao redor da cápsula.</summary>
    private void PlayAlertPulse(string activityId)
    {
        var now = Environment.TickCount64;
        var repeated = activityId == _lastPulseId && now - _lastPulseTicks < PulseRepeatWindow.TotalMilliseconds;
        _lastPulseId = activityId;
        _lastPulseTicks = now;

        if (repeated || !ShouldAnimate || !IslandHost.IsVisible)
        {
            return;
        }

        var duration = Scaled(PulseMilliseconds);
        AlertRing.BeginAnimation(OpacityProperty, Frozen(new DoubleAnimation(PulseOpacity, 0, duration) { EasingFunction = SoftEasing }));
        var grow = Frozen(new DoubleAnimation(1, PulseScale, duration) { EasingFunction = SoftEasing });
        AlertRingScale.BeginAnimation(ScaleTransform.ScaleXProperty, grow);
        AlertRingScale.BeginAnimation(ScaleTransform.ScaleYProperty, grow);
    }

    private void AnimateHover(bool hovering)
    {
        var scale = hovering ? HoverScale : 1.0;

        if (!ShouldAnimate)
        {
            SetImmediate(HoverScaleTransform, ScaleTransform.ScaleXProperty, scale);
            SetImmediate(HoverScaleTransform, ScaleTransform.ScaleYProperty, scale);
            return;
        }

        var duration = Scaled(HoverMilliseconds);
        Animate(HoverScaleTransform, ScaleTransform.ScaleXProperty, scale, duration, SoftEasing);
        Animate(HoverScaleTransform, ScaleTransform.ScaleYProperty, scale, duration, SoftEasing);
    }

    private bool IsInsideInteractiveControl(DependencyObject? element)
    {
        while (element is not null && element != IslandHost)
        {
            if (element is ButtonBase or RangeBase or Thumb)
            {
                return true;
            }

            element = element is Visual or System.Windows.Media.Media3D.Visual3D
                ? VisualTreeHelper.GetParent(element)
                : LogicalTreeHelper.GetParent(element);
        }

        return false;
    }

    private ContentPresenter LayerFor(IslandState state) => state switch
    {
        IslandState.Expanded => ExpandedLayer,
        IslandState.Interactive => InteractiveLayer,
        _ => CompactLayer,
    };

    private TimeSpan Scaled(double milliseconds) => TimeSpan.FromMilliseconds(milliseconds / Speed);

    private static void Animate(
        IAnimatable target,
        DependencyProperty property,
        double to,
        TimeSpan duration,
        IEasingFunction? easing,
        TimeSpan beginTime = default)
    {
        target.BeginAnimation(property, Frozen(new DoubleAnimation(to, new Duration(duration))
        {
            EasingFunction = easing,
            BeginTime = beginTime,
        }));
    }

    private static void SetImmediate(DependencyObject target, DependencyProperty property, double value)
    {
        ((IAnimatable)target).BeginAnimation(property, null);
        target.SetValue(property, value);
    }

    /// <summary>Easing da expansão conforme a intensidade configurada (0 = sem efeito de mola).</summary>
    private IEasingFunction ExpandEasing()
    {
        var intensity = _settings.Current.AnimationIntensity;
        if (intensity != _expandEasingIntensity)
        {
            _expandEasingIntensity = intensity;
            _expandEasing = intensity <= 0.01
                ? NoSpringEasing
                : Frozen(new BackEase { Amplitude = BaseSpringAmplitude * intensity, EasingMode = EasingMode.EaseOut });
        }

        return _expandEasing;
    }

    private static T Frozen<T>(T freezable) where T : Freezable
    {
        freezable.Freeze();
        return freezable;
    }
}
