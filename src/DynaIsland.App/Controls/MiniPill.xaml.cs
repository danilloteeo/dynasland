using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace DynaIsland.App.Controls;

/// <summary>Mini pílula do DynaIsland, usada como marca nas janelas (Novidades, estados vazios).</summary>
public partial class MiniPill : UserControl
{
    private const double RestWidth = 104;
    private const double OpenWidth = 124;

    public MiniPill()
    {
        InitializeComponent();
        Time.Text = DateTime.Now.ToString("HH:mm");
    }

    /// <summary>Abre um pouco e volta, com desaceleração suave (sem efeito se as animações do Windows estão desligadas).</summary>
    public void Pulse()
    {
        if (!SystemParameters.ClientAreaAnimation)
        {
            return;
        }

        var grow = new DoubleAnimation(RestWidth, OpenWidth, TimeSpan.FromMilliseconds(260))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
            AutoReverse = true,
        };
        Capsule.BeginAnimation(WidthProperty, grow);
    }
}
