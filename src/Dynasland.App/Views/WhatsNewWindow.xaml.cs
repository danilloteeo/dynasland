using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using Dynasland.App.Services;
using Dynasland.Core.Services;

namespace Dynasland.App.Views;

/// <summary>Novidades da versão, mostradas uma vez depois de atualizar.</summary>
public partial class WhatsNewWindow : Window
{
    private readonly IReadOnlyList<WhatsNewPage> _pages;
    private int _index;

    public WhatsNewWindow(IReadOnlyList<WhatsNewPage> pages, ThemeService theme)
    {
        InitializeComponent();
        TitleBarTheme.Attach(this, theme);
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
        RebuildDots();

        if (animate && SystemParameters.ClientAreaAnimation)
        {
            PageContent.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(300))
            {
                EasingFunction = new ExponentialEase { Exponent = 6, EasingMode = EasingMode.EaseOut },
            });
        }
    }

    private void RebuildDots()
    {
        Dots.Children.Clear();
        for (var i = 0; i < _pages.Count; i++)
        {
            // A página atual vira pílula (16x6); as demais são pontos (6x6).
            Dots.Children.Add(new Rectangle
            {
                Width = i == _index ? 16 : 6,
                Height = 6,
                RadiusX = 3,
                RadiusY = 3,
                Margin = new Thickness(0, 0, 6, 0),
                Fill = (Brush)FindResource(i == _index ? "SettingsTextBrush" : "SettingsTrackBrush"),
            });
        }
    }
}
