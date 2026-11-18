using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using DynaIsland.App.Services;
using DynaIsland.Core.Services;

namespace DynaIsland.App.Views;

/// <summary>Novidades da versão, mostradas uma vez depois de atualizar.</summary>
public partial class WhatsNewWindow : Window
{
    private static readonly IEasingFunction Ease = new CubicEase { EasingMode = EasingMode.EaseOut };
    private static readonly Duration SwapDuration = TimeSpan.FromMilliseconds(240);

    private readonly IReadOnlyList<WhatsNewPage> _pages;
    private int _index;

    public WhatsNewWindow(IReadOnlyList<WhatsNewPage> pages, ThemeService theme)
    {
        InitializeComponent();
        TitleBarTheme.Attach(this, theme);
        // Windows 11: acrílico com véu escuro por cima; antes disso, o fundo sólido da paleta.
        if (WindowBackdrop.TryApply(this))
        {
            Veil.Background = new SolidColorBrush(Color.FromArgb(0x9E, 0x16, 0x1A, 0x28));
        }

        _pages = pages;
        Show(0, animate: false);
    }

    private void OnNext(object sender, RoutedEventArgs e)
    {
        if (_index >= _pages.Count - 1)
        {
            Close();
            return;
        }

        Show(_index + 1, animate: true);
    }

    private void Show(int index, bool animate)
    {
        _index = index;
        PageTitle.Text = _pages[index].Title;
        PageText.Text = _pages[index].Text;
        NextButton.Content = index == _pages.Count - 1 ? "Começar" : "Próximo";
        RebuildDots(animate);

        if (animate && SystemParameters.ClientAreaAnimation)
        {
            PageContent.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, SwapDuration) { EasingFunction = Ease });
            PageContent.RenderTransform.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(12, 0, SwapDuration) { EasingFunction = Ease });
            Pill.Pulse();
        }
    }

    /// <summary>Bolinhas de 6 px; a da página atual vira um traço de 16 px (animado ao avançar).</summary>
    private void RebuildDots(bool animate)
    {
        Dots.Children.Clear();
        for (var i = 0; i < _pages.Count; i++)
        {
            var active = i == _index;
            var dot = new Border
            {
                Width = active && !animate ? 16 : 6,
                Height = 6,
                CornerRadius = new CornerRadius(3),
                Margin = new Thickness(0, 0, 6, 0),
                Background = (Brush)FindResource(active ? "SettingsTextBrush" : "SettingsTrackBrush"),
            };
            Dots.Children.Add(dot);
            if (active && animate)
            {
                dot.BeginAnimation(WidthProperty, new DoubleAnimation(6, 16, SwapDuration) { EasingFunction = Ease });
            }
        }
    }
}
