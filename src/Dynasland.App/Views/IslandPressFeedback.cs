using System.Windows;

namespace Dynasland.App.Views;

/// <summary>
/// Liga ou desliga a resposta ao toque (encolher ao pressionar) dos botões da ilha. Herdável: a janela da
/// ilha define o valor uma vez e todos os botões dos templates o recebem. Sem animação, o botão pressionado
/// só troca o fundo.
/// </summary>
public static class IslandPressFeedback
{
    public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.RegisterAttached(
        "IsEnabled",
        typeof(bool),
        typeof(IslandPressFeedback),
        new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.Inherits));

    public static bool GetIsEnabled(DependencyObject element) => (bool)element.GetValue(IsEnabledProperty);

    public static void SetIsEnabled(DependencyObject element, bool value) => element.SetValue(IsEnabledProperty, value);
}
