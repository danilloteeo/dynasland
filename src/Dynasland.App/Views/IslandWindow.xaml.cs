using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Dynasland.App.Services;
using Dynasland.App.ViewModels;
using Dynasland.Core.Interfaces;
using Dynasland.Core.Layout;
using Dynasland.Core.Models;
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
    private const double HoverMilliseconds = 160;
    private const double HoverScale = 1.05;

    // Leve overshoot na expansão (sensação de mola) e desaceleração suave no recolhimento.
    private static readonly IEasingFunction ExpandEasing = Frozen(new BackEase { Amplitude = 0.22, EasingMode = EasingMode.EaseOut });
    private static readonly IEasingFunction CollapseEasing = Frozen(new CubicEase { EasingMode = EasingMode.EaseInOut });
    private static readonly IEasingFunction HoverEasing = Frozen(new QuadraticEase { EasingMode = EasingMode.EaseOut });

    private readonly IslandViewModel _viewModel;
    private readonly IslandPlacementService _placement;
    private readonly IIslandSettingsService _settings;
    private bool _repositionPending;

    public IslandWindow(IslandViewModel viewModel, IslandPlacementService placement, IIslandSettingsService settings)
    {
        _viewModel = viewModel;
        _placement = placement;
        _settings = settings;

        InitializeComponent();
        DataContext = viewModel;

        ApplyState(viewModel.State, animate: false);
        viewModel.PropertyChanged += OnViewModelPropertyChanged;
        settings.Changed += OnSettingsChanged;
    }

    public double CornerSize
    {
        get => (double)GetValue(CornerSizeProperty);
        set => SetValue(CornerSizeProperty, value);
    }

    /// <summary>Cria o HWND, aplica estilos Win32, posiciona e exibe (se configurado) sem roubar o foco.</summary>
    public void Start()
    {
        new WindowInteropHelper(this).EnsureHandle();
        ApplyVisibility(_settings.Current.IsVisible);
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
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        _settings.Changed -= OnSettingsChanged;
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

        ApplyVisibility(settings.IsVisible);
        RequestReposition();
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
            _viewModel.State = IslandState.Compact;
            Hide();
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(IslandViewModel.State))
        {
            ApplyState(_viewModel.State, animate: IsVisible);
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
        AnimateHover(false);
        _viewModel.ToggleInteractiveCommand.Execute(null);
        e.Handled = true;
    }

    private void ApplyState(IslandState state, bool animate)
    {
        var target = IslandLayout.For(state);
        var settings = _settings.Current;
        var showDetails = state == IslandState.Interactive;
        InteractiveContent.IsHitTestVisible = showDetails;

        if (!animate || !settings.AnimationsEnabled)
        {
            SetImmediate(IslandHost, WidthProperty, target.Width);
            SetImmediate(IslandHost, HeightProperty, target.Height);
            SetImmediate(this, CornerSizeProperty, target.CornerRadius);
            SetImmediate(InteractiveContent, OpacityProperty, showDetails ? 1 : 0);
            return;
        }

        var speed = Math.Clamp(settings.AnimationSpeed, 0.25, 3);
        var growing = target.Height >= IslandHost.Height;
        var duration = TimeSpan.FromMilliseconds((growing ? ExpandMilliseconds : CollapseMilliseconds) / speed);
        var easing = growing ? ExpandEasing : CollapseEasing;

        Animate(IslandHost, WidthProperty, target.Width, duration, easing);
        Animate(IslandHost, HeightProperty, target.Height, duration, easing);
        Animate(this, CornerSizeProperty, target.CornerRadius, duration, easing);

        if (showDetails)
        {
            // O conteúdo aparece depois que a cápsula já ganhou espaço, evitando corte do texto.
            Animate(InteractiveContent, OpacityProperty, 1, duration * 0.6, null, beginTime: duration * 0.4);
        }
        else
        {
            Animate(InteractiveContent, OpacityProperty, 0, TimeSpan.FromMilliseconds(120 / speed), null);
        }
    }

    private void AnimateHover(bool hovering)
    {
        var scale = hovering ? HoverScale : 1.0;
        var settings = _settings.Current;

        if (!settings.AnimationsEnabled)
        {
            SetImmediate(HoverScaleTransform, System.Windows.Media.ScaleTransform.ScaleXProperty, scale);
            SetImmediate(HoverScaleTransform, System.Windows.Media.ScaleTransform.ScaleYProperty, scale);
            return;
        }

        var duration = TimeSpan.FromMilliseconds(HoverMilliseconds / Math.Clamp(settings.AnimationSpeed, 0.25, 3));
        Animate(HoverScaleTransform, System.Windows.Media.ScaleTransform.ScaleXProperty, scale, duration, HoverEasing);
        Animate(HoverScaleTransform, System.Windows.Media.ScaleTransform.ScaleYProperty, scale, duration, HoverEasing);
    }

    private static void Animate(
        IAnimatable target,
        DependencyProperty property,
        double to,
        TimeSpan duration,
        IEasingFunction? easing,
        TimeSpan beginTime = default)
    {
        var animation = new DoubleAnimation(to, new Duration(duration))
        {
            EasingFunction = easing,
            BeginTime = beginTime,
        };
        animation.Freeze();
        target.BeginAnimation(property, animation);
    }

    private static void SetImmediate(DependencyObject target, DependencyProperty property, double value)
    {
        ((IAnimatable)target).BeginAnimation(property, null);
        target.SetValue(property, value);
    }

    private static T Frozen<T>(T freezable) where T : Freezable
    {
        freezable.Freeze();
        return freezable;
    }
}
