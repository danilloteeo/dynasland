using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Dynasland.App.Controls;

/// <summary>
/// Entrada em cascata das linhas de uma lista: cada uma surge com fade e sobe 8 px, com 30 ms de atraso
/// entre linhas (limitado). Roda uma única vez por elemento, na primeira carga.
/// </summary>
public static class RowCascade
{
    private const int MaxStaggeredRows = 10;
    private static readonly TimeSpan Stagger = TimeSpan.FromMilliseconds(30);
    private static readonly Duration AnimationDuration = new(TimeSpan.FromMilliseconds(300));

    public static readonly DependencyProperty EnabledProperty = DependencyProperty.RegisterAttached(
        "Enabled", typeof(bool), typeof(RowCascade), new PropertyMetadata(false, OnEnabledChanged));

    private static readonly DependencyProperty PlayedProperty = DependencyProperty.RegisterAttached(
        "Played", typeof(bool), typeof(RowCascade), new PropertyMetadata(false));

    public static bool GetEnabled(DependencyObject element) => (bool)element.GetValue(EnabledProperty);

    public static void SetEnabled(DependencyObject element, bool value) => element.SetValue(EnabledProperty, value);

    /// <summary>Atraso de entrada da linha de posição <paramref name="index"/>: 30 ms por linha, até 10 linhas.</summary>
    public static TimeSpan DelayFor(int index) => TimeSpan.FromTicks(Stagger.Ticks * Math.Clamp(index, 0, MaxStaggeredRows));

    /// <summary>
    /// Toca a entrada de um elemento que não é item de lista (as linhas do painel da ilha), na posição
    /// <paramref name="index"/> da cascata e depois de <paramref name="extraDelay"/>. Uma vez por elemento.
    /// </summary>
    public static void Play(FrameworkElement element, int index, TimeSpan extraDelay)
    {
        if ((bool)element.GetValue(PlayedProperty)) return;
        element.SetValue(PlayedProperty, true);
        if (!SystemParameters.ClientAreaAnimation) return;

        var delay = DelayFor(index) + extraDelay;
        Fade(element, delay);
        Slide(element, delay);
    }

    private static void OnEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not FrameworkElement element) return;
        element.Loaded -= OnLoaded;
        if ((bool)e.NewValue)
        {
            element.Loaded += OnLoaded;
            if (element.IsLoaded) OnLoaded(element, new RoutedEventArgs());
        }
    }

    private static void OnLoaded(object sender, RoutedEventArgs e)
    {
        var element = (FrameworkElement)sender;
        if ((bool)element.GetValue(PlayedProperty)) return;
        element.SetValue(PlayedProperty, true);
        if (!SystemParameters.ClientAreaAnimation) return;

        var delay = DelayFor(IndexOf(element));
        Fade(element, delay);
        Slide(element, delay);
    }

    private static void Fade(FrameworkElement element, TimeSpan delay)
    {
        var source = DependencyPropertyHelper.GetValueSource(element, UIElement.OpacityProperty);
        if (source.IsExpression || source.IsAnimated) return;

        var final = element.Opacity;
        element.Opacity = 0;
        var fade = new DoubleAnimation(0, final, AnimationDuration)
        {
            BeginTime = delay,
            EasingFunction = new ExponentialEase { Exponent = 6, EasingMode = EasingMode.EaseOut },
            FillBehavior = FillBehavior.Stop,
        };
        fade.Completed += (_, _) =>
        {
            element.BeginAnimation(UIElement.OpacityProperty, null);
            element.Opacity = final;
        };
        element.BeginAnimation(UIElement.OpacityProperty, fade);
    }

    private static void Slide(FrameworkElement element, TimeSpan delay)
    {
        var existing = element.RenderTransform;
        if (existing is not null && !existing.Value.IsIdentity) return;

        var translate = new TranslateTransform(0, 8);
        element.RenderTransform = translate;
        var slide = new DoubleAnimation(8, 0, AnimationDuration)
        {
            BeginTime = delay,
            EasingFunction = new ExponentialEase { Exponent = 6, EasingMode = EasingMode.EaseOut },
            FillBehavior = FillBehavior.Stop,
        };
        slide.Completed += (_, _) =>
        {
            translate.BeginAnimation(TranslateTransform.YProperty, null);
            translate.Y = 0;
            if (ReferenceEquals(element.RenderTransform, translate)) element.ClearValue(UIElement.RenderTransformProperty);
        };
        translate.BeginAnimation(TranslateTransform.YProperty, slide);
    }

    private static int IndexOf(FrameworkElement element)
    {
        var owner = ItemsControl.ItemsControlFromItemContainer(element);
        if (owner is not null)
        {
            var index = owner.ItemContainerGenerator.IndexFromContainer(element);
            if (index >= 0) return index;
        }

        if (VisualTreeHelper.GetParent(element) is Panel panel)
        {
            var index = panel.Children.IndexOf(element);
            if (index >= 0) return index;
        }

        return 0;
    }
}
