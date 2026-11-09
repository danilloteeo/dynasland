using System.Windows;
using System.Windows.Controls;
using Dynasland.Core.Models;

namespace Dynasland.App.Views;

/// <summary>
/// Escolhe o DataTemplate de um conteúdo de atividade pela chave <c>"{NomeCompletoDoTipo}:{Estado}"</c>.
/// O estado interativo recorre ao template expandido quando o módulo não define um próprio.
/// Sem template, nada é desenhado (em vez do ToString padrão do WPF).
/// </summary>
public sealed class ActivityTemplateSelector : DataTemplateSelector
{
    private static readonly DataTemplate EmptyTemplate = CreateEmptyTemplate();

    public IslandState Mode { get; set; }

    public override DataTemplate SelectTemplate(object? item, DependencyObject container)
    {
        if (item is null || container is not FrameworkElement element)
        {
            return EmptyTemplate;
        }

        var typeName = item.GetType().FullName;
        foreach (var mode in Candidates(Mode))
        {
            if (element.TryFindResource($"{typeName}:{mode}") is DataTemplate template)
            {
                return template;
            }
        }

        return EmptyTemplate;
    }

    private static IslandState[] Candidates(IslandState mode) => mode switch
    {
        IslandState.Interactive => [IslandState.Interactive, IslandState.Expanded],
        _ => [mode],
    };

    private static DataTemplate CreateEmptyTemplate()
    {
        var template = new DataTemplate { VisualTree = new FrameworkElementFactory(typeof(Grid)) };
        template.Seal();
        return template;
    }
}
