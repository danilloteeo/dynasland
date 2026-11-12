using System.Windows;
using System.Windows.Controls;

namespace Dynasland.App.Controls;

/// <summary>Estado vazio das listas da Central: mascote em pixel art e uma frase.</summary>
public partial class EmptyState : UserControl
{
    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
        nameof(Text), typeof(string), typeof(EmptyState), new PropertyMetadata(string.Empty));

    public EmptyState() => InitializeComponent();

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }
}
