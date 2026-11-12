using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Dynasland.App.Controls;
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

    /// <summary>Formato da ilha: encaixada deixa os cantos de cima da cápsula retos (IslandCornerRadiusConverter).</summary>
    public static readonly DependencyProperty ShapeProperty = DependencyProperty.Register(
        nameof(Shape),
        typeof(IslandShape),
        typeof(IslandWindow),
        new PropertyMetadata(IslandShape.Float));

    private const double ExpandMilliseconds = 460;
    private const double CollapseMilliseconds = 340;
    private const double FadeOutMilliseconds = 120;
    private const double SwapMilliseconds = 240;
    private const double HoverMilliseconds = 160;
    private const double HoverScale = 1.05;
    private const double EnterMilliseconds = 300;
    private const double LeaveMilliseconds = 120;
    private const double EnterScale = 0.965;
    private const double EnterRise = 10;
    private const double LeaveScale = 0.98;
    private const double LeaveRise = 4;
    private const double SwapOffset = 6;
    private const double GhostRise = -4;
    private const double PrivacyDotFadeMs = 150;
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
    // Mola do Volum: cubic-bezier(.16, 1, .3, 1) na entrada; saída curta que acelera.
    private static readonly IEasingFunction SpringOut = Frozen(new ExponentialEase { Exponent = 6, EasingMode = EasingMode.EaseOut });
    private static readonly IEasingFunction LeaveEase = Frozen(new QuadraticEase { EasingMode = EasingMode.EaseIn });
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
    private bool _wantsVisible;
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

        ApplyPositionAndShape(settings.Current);
        ApplyLayers(viewModel.State, animate: false);
        ApplyPrivacyDot(MicrophoneDot, viewModel.ShowMicrophoneDot, animate: false);
        ApplyPrivacyDot(CameraDot, viewModel.ShowCameraDot, animate: false);
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

    public IslandShape Shape
    {
        get => (IslandShape)GetValue(ShapeProperty);
        set => SetValue(ShapeProperty, value);
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
        ApplyPositionAndShape(settings);
        ApplyDimensions(_viewModel.Dimensions, ShouldAnimate);
        ApplyPressFeedback();
        RequestReposition();
    }

    /// <summary>
    /// A janela tem largura fixa; a cápsula se alinha ao lado da posição escolhida (a <see cref="IslandPlacementCalculator.EdgeMargin"/>
    /// da borda), então a borda ancorada fica parada enquanto a largura anima. O zoom do hover parte do mesmo canto.
    /// </summary>
    private void ApplyPositionAndShape(IslandSettings settings)
    {
        Shape = settings.Shape;
        var edge = IslandPlacementCalculator.EdgeMargin;
        (IslandHost.HorizontalAlignment, IslandHost.Margin, IslandHost.RenderTransformOrigin) = settings.Position switch
        {
            IslandPosition.Left => (HorizontalAlignment.Left, new Thickness(edge, 0, 0, 0), new Point(0, 0)),
            IslandPosition.Right => (HorizontalAlignment.Right, new Thickness(0, 0, edge, 0), new Point(1, 0)),
            _ => (HorizontalAlignment.Center, new Thickness(0), new Point(0.5, 0)),
        };
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
        _wantsVisible = visible;
        if (visible && !IsVisible)
        {
            SetStage(0, EnterScale, EnterRise);
            Show();
            _placement.Place(this);
            PlayStage(1, 1, 0, Scaled(EnterMilliseconds), SpringOut);
        }
        else if (visible)
        {
            // Voltou a aparecer no meio da saída: retoma a entrada de onde estava.
            PlayStage(1, 1, 0, Scaled(EnterMilliseconds), SpringOut);
        }
        else if (IsVisible && ShouldAnimate)
        {
            PlayStage(0, LeaveScale, LeaveRise, Scaled(LeaveMilliseconds), LeaveEase, () =>
            {
                if (!_wantsVisible && IsVisible)
                {
                    Hide();
                }
            });
        }
        else if (IsVisible)
        {
            Hide();
        }

        _viewModel.SetWindowVisible(visible);
    }

    private void SetStage(double opacity, double scale, double rise)
    {
        SetImmediate(Stage, OpacityProperty, opacity);
        SetImmediate(StageScale, ScaleTransform.ScaleXProperty, scale);
        SetImmediate(StageScale, ScaleTransform.ScaleYProperty, scale);
        SetImmediate(StageShift, TranslateTransform.YProperty, rise);
    }

    /// <summary>Anima o palco até o estado dado; sem animação, vai direto. <paramref name="done"/> roda ao fim.</summary>
    private void PlayStage(double opacity, double scale, double rise, TimeSpan duration, IEasingFunction easing, Action? done = null)
    {
        if (!ShouldAnimate)
        {
            SetStage(opacity, scale, rise);
            done?.Invoke();
            return;
        }

        Animate(Stage, OpacityProperty, opacity, duration, easing);
        Animate(StageScale, ScaleTransform.ScaleXProperty, scale, duration, easing);
        Animate(StageScale, ScaleTransform.ScaleYProperty, scale, duration, easing);
        var shift = new DoubleAnimation(rise, new Duration(duration)) { EasingFunction = easing };
        if (done is not null)
        {
            shift.Completed += (_, _) => done();
        }
        else
        {
            shift.Freeze();
        }

        StageShift.BeginAnimation(TranslateTransform.YProperty, shift);
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

            case nameof(IslandViewModel.ShowMicrophoneDot):
                ApplyPrivacyDot(MicrophoneDot, _viewModel.ShowMicrophoneDot, ShouldAnimate);
                break;

            case nameof(IslandViewModel.ShowCameraDot):
                ApplyPrivacyDot(CameraDot, _viewModel.ShowCameraDot, ShouldAnimate);
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

    /// <summary>
    /// Mostra ou oculta um indicador de privacidade com um fade curto. Oculto, sai do layout ao fim do fade,
    /// para o outro ponto ficar centralizado sozinho.
    /// </summary>
    private void ApplyPrivacyDot(UIElement dot, bool show, bool animate)
    {
        var target = show ? 1.0 : 0.0;
        if (show)
        {
            dot.Visibility = Visibility.Visible;
        }

        if (!animate)
        {
            dot.BeginAnimation(OpacityProperty, null);
            dot.Opacity = target;
            dot.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
            return;
        }

        var fade = new DoubleAnimation(target, TimeSpan.FromMilliseconds(PrivacyDotFadeMs / Speed));
        if (!show)
        {
            fade.Completed += (_, _) =>
            {
                if (dot.Opacity == 0)
                {
                    dot.Visibility = Visibility.Collapsed;
                }
            };
        }

        dot.BeginAnimation(OpacityProperty, fade);
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

        if (interactive && animate)
        {
            CascadeRows(InteractiveLayer, resizeDuration * 0.4);
        }

        if (state != IslandState.Compact)
        {
            AnimateHover(false);
        }
    }

    /// <summary>
    /// Entrada em cascata das linhas do painel interativo (como a lista do Volum): depois do layout, cada filho
    /// direto do painel raiz do modelo surge em ordem de leitura, 30 ms depois do anterior.
    /// </summary>
    private void CascadeRows(ContentPresenter layer, TimeSpan extraDelay)
    {
        Dispatcher.BeginInvoke(() =>
        {
            if (FindRootPanel(layer) is not { } panel)
            {
                return;
            }

            var rows = panel.Children.OfType<FrameworkElement>()
                .Select((element, order) => (element, order))
                .OrderBy(r => panel is Grid ? Grid.GetRow(r.element) : 0)
                .ThenBy(r => panel is Grid ? Grid.GetColumn(r.element) : 0)
                .ThenBy(r => r.order)
                .Select(r => r.element)
                .ToList();
            for (var i = 0; i < rows.Count; i++)
            {
                RowCascade.Play(rows[i], i, extraDelay);
            }
        }, DispatcherPriority.Loaded);
    }

    private static Panel? FindRootPanel(DependencyObject parent)
    {
        for (var depth = 0; depth < 6 && VisualTreeHelper.GetChildrenCount(parent) > 0; depth++)
        {
            parent = VisualTreeHelper.GetChild(parent, 0);
            if (parent is Panel panel && panel.Children.Count > 1)
            {
                return panel;
            }
        }

        return null;
    }

    private void PlaySwap(ContentPresenter layer)
    {
        if (!ShouldAnimate)
        {
            RemoveGhost();
            return;
        }

        var duration = Scaled(SwapMilliseconds);
        if (layer == InteractiveLayer)
        {
            CascadeRows(layer, TimeSpan.Zero);
        }

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
