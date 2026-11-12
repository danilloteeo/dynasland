using System.Windows;
using System.Windows.Media.Animation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace Dynasland.App.Controls;

/// <summary>
/// Grupo de opções curtas (RadioButtons) com uma pílula de destaque que desliza até a opção marcada.
/// O conteúdo é um UniformGrid de RadioButtons com o estilo SegmentItem.
/// </summary>
public sealed class SegmentedControl : ContentControl
{
    // Recuo do trilho: 1 de borda + 3 de folga. O template posiciona pílula e conteúdo a partir da borda externa.
    private const double Inset = 4;

    private FrameworkElement? _pill;
    private bool _positioned;

    static SegmentedControl() =>
        DefaultStyleKeyProperty.OverrideMetadata(typeof(SegmentedControl), new FrameworkPropertyMetadata(typeof(SegmentedControl)));

    public SegmentedControl()
    {
        AddHandler(ToggleButton.CheckedEvent, new RoutedEventHandler((_, _) => UpdatePill(animate: true)));
        SizeChanged += (_, _) => UpdatePill(animate: false);
        Loaded += (_, _) => UpdatePill(animate: false);
    }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        _pill = GetTemplateChild("PART_Pill") as FrameworkElement;
        _positioned = false;
    }

    private void UpdatePill(bool animate)
    {
        if (_pill is null || Content is not Panel items || ActualWidth <= 0)
        {
            return;
        }

        var index = items.Children.OfType<ToggleButton>().ToList().FindIndex(b => b.IsChecked == true);
        var (left, width) = SegmentedLayout.Pill(items.Children.Count, index, ActualWidth, Inset);

        _pill.Visibility = width > 0 ? Visibility.Visible : Visibility.Collapsed;
        _pill.Width = width;

        var target = new Thickness(left, 0, 0, 0);
        var motion = animate && _positioned && SystemParameters.ClientAreaAnimation;
        if (motion)
        {
            _pill.BeginAnimation(FrameworkElement.MarginProperty, new ThicknessAnimation(target, TimeSpan.FromMilliseconds(300))
            {
                EasingFunction = new ExponentialEase { Exponent = 6, EasingMode = EasingMode.EaseOut },
            });
        }
        else
        {
            _pill.BeginAnimation(FrameworkElement.MarginProperty, null);
            _pill.Margin = target;
        }

        _positioned = width > 0;
    }
}
