using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace Dynasland.App.Controls;

/// <summary>
/// Painel recolhível: recorta o conteúdo e anima a própria altura entre 0 e a altura desejada do conteúdo.
/// </summary>
public class CollapsePanel : ContentControl
{
    public static readonly TimeSpan OpenDuration = TimeSpan.FromMilliseconds(300);
    public static readonly TimeSpan CloseDuration = TimeSpan.FromMilliseconds(120);

    public static TimeSpan Durations(bool opening) => opening ? OpenDuration : CloseDuration;

    public static readonly DependencyProperty IsExpandedProperty = DependencyProperty.Register(
        nameof(IsExpanded), typeof(bool), typeof(CollapsePanel),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnIsExpandedChanged));

    private bool _initialized;

    public CollapsePanel()
    {
        ClipToBounds = true;
        Height = 0;
        Focusable = false;
        IsTabStop = false;
        Loaded += (_, _) =>
        {
            if (_initialized) return;
            _initialized = true;
            Apply(IsExpanded, animate: false);
        };
    }

    public bool IsExpanded
    {
        get => (bool)GetValue(IsExpandedProperty);
        set => SetValue(IsExpandedProperty, value);
    }

    private static void OnIsExpandedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var panel = (CollapsePanel)d;
        if (!panel._initialized) return;
        panel.Apply((bool)e.NewValue, SystemParameters.ClientAreaAnimation);
    }

    private double MeasureContentHeight()
    {
        if (Content is not UIElement content) return 0;
        var width = ActualWidth > 0 ? ActualWidth : double.PositiveInfinity;
        // Mede via o próprio template (ContentPresenter) com altura infinita.
        content.Measure(new Size(width, double.PositiveInfinity));
        return content.DesiredSize.Height + Padding.Top + Padding.Bottom;
    }

    private void Apply(bool expanded, bool animate)
    {
        var content = Content as UIElement;
        BeginAnimation(HeightProperty, null);
        content?.BeginAnimation(OpacityProperty, null);

        if (expanded)
        {
            if (content is not null) content.Visibility = Visibility.Visible;
            if (!animate)
            {
                Height = double.NaN;
                if (content is not null) content.Opacity = 1;
                return;
            }

            var target = MeasureContentHeight();
            var from = double.IsNaN(Height) ? 0 : Height;
            var anim = new DoubleAnimation(from, target, Durations(true))
            {
                EasingFunction = new ExponentialEase { Exponent = 6, EasingMode = EasingMode.EaseOut },
                FillBehavior = FillBehavior.Stop
            };
            anim.Completed += (_, _) =>
            {
                if (!IsExpanded) return;
                Height = double.NaN;
            };
            Height = from;
            BeginAnimation(HeightProperty, anim);
            content?.BeginAnimation(OpacityProperty, new DoubleAnimation(content.Opacity, 1, Durations(true)));
        }
        else
        {
            if (!animate)
            {
                Height = 0;
                if (content is not null) { content.Opacity = 0; content.Visibility = Visibility.Collapsed; }
                return;
            }

            var from = double.IsNaN(Height) ? ActualHeight : Height;
            Height = from;
            var anim = new DoubleAnimation(from, 0, Durations(false))
            {
                EasingFunction = new ExponentialEase { Exponent = 6, EasingMode = EasingMode.EaseOut },
                FillBehavior = FillBehavior.Stop
            };
            anim.Completed += (_, _) =>
            {
                if (IsExpanded) return;
                Height = 0;
                if (content is not null) content.Visibility = Visibility.Collapsed;
            };
            BeginAnimation(HeightProperty, anim);
            content?.BeginAnimation(OpacityProperty, new DoubleAnimation(content.Opacity, 0, Durations(false)));
        }
    }
}
