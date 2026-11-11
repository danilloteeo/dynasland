namespace Dynasland.Core.Modules;

/// <summary>
/// Cor que um módulo sugere para o fundo da ilha (ex.: a capa da música tocando). A aparência decide
/// se a usa, conforme as preferências.
/// </summary>
public interface IIslandTint
{
    /// <summary>Cor em hexadecimal (#RRGGBB); <c>null</c> = sem sugestão.</summary>
    string? Color { get; }

    /// <summary>Pode ser disparado em qualquer thread.</summary>
    event EventHandler? Changed;

    void Set(string? color);
}

public sealed class IslandTint : IIslandTint
{
    private volatile string? _color;

    public string? Color => _color;

    public event EventHandler? Changed;

    public void Set(string? color)
    {
        if (string.Equals(_color, color, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _color = color;
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
