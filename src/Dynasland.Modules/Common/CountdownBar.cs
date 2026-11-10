using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Dynasland.Modules.Common;

/// <summary>Tempo de exibição de um conteúdo. Cada conteúdo cria a sua instância, o que reinicia a barra.</summary>
public sealed class CountdownSpan(TimeSpan duration)
{
    public TimeSpan Duration { get; } = duration;
}

/// <summary>
/// Faz um elemento encolher da largura total até zero ao longo de um <see cref="CountdownSpan"/>,
/// pausando enquanto o ponteiro está sobre a janela.
/// </summary>
public static class CountdownBar
{
    public static readonly DependencyProperty SpanProperty = DependencyProperty.RegisterAttached(
        "Span", typeof(CountdownSpan), typeof(CountdownBar), new PropertyMetadata(null, OnSpanChanged));

    private static readonly DependencyProperty StateProperty = DependencyProperty.RegisterAttached(
        "State", typeof(State), typeof(CountdownBar));

    public static CountdownSpan? GetSpan(DependencyObject element) => (CountdownSpan?)element.GetValue(SpanProperty);

    public static void SetSpan(DependencyObject element, CountdownSpan? value) => element.SetValue(SpanProperty, value);

    private static void OnSpanChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not FrameworkElement element)
        {
            return;
        }

        if (element.GetValue(StateProperty) is not State state)
        {
            state = new State(element);
            element.SetValue(StateProperty, state);
        }

        state.Restart(e.NewValue as CountdownSpan);
    }

    private sealed class State
    {
        private readonly FrameworkElement _element;
        private readonly ScaleTransform _scale = new(1, 1);
        private Window? _window;
        private AnimationClock? _clock;

        public State(FrameworkElement element)
        {
            _element = element;
            element.RenderTransformOrigin = new Point(0, 0.5);
            element.RenderTransform = _scale;
            element.Loaded += (_, _) => Attach();
            element.Unloaded += (_, _) => Detach();
            if (element.IsLoaded)
            {
                Attach();
            }
        }

        public void Restart(CountdownSpan? span)
        {
            _clock = null;
            if (span is null || span.Duration <= TimeSpan.Zero)
            {
                _scale.ApplyAnimationClock(ScaleTransform.ScaleXProperty, null);
                return;
            }

            _clock = new DoubleAnimation(1, 0, span.Duration) { FillBehavior = FillBehavior.HoldEnd }.CreateClock();
            _scale.ApplyAnimationClock(ScaleTransform.ScaleXProperty, _clock);
            if (_window?.IsMouseOver == true)
            {
                _clock.Controller?.Pause();
            }
        }

        private void Attach()
        {
            Detach();
            _window = Window.GetWindow(_element);
            if (_window is not null)
            {
                _window.MouseEnter += OnMouseEnter;
                _window.MouseLeave += OnMouseLeave;
            }
        }

        private void Detach()
        {
            if (_window is not null)
            {
                _window.MouseEnter -= OnMouseEnter;
                _window.MouseLeave -= OnMouseLeave;
                _window = null;
            }
        }

        private void OnMouseEnter(object sender, MouseEventArgs e) => _clock?.Controller?.Pause();

        private void OnMouseLeave(object sender, MouseEventArgs e) => _clock?.Controller?.Resume();
    }
}
