namespace Dynasland.Modules;

/// <summary>Dicionário de recursos (DataTemplates) de um módulo, mesclado pela aplicação na inicialização.</summary>
public sealed record ModuleTemplates(Uri Source)
{
    public static ModuleTemplates FromComponent(string relativePath) =>
        new(new Uri($"pack://application:,,,/Dynasland.Modules;component/{relativePath}", UriKind.Absolute));
}
