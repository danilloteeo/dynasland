namespace Dynasland.Core.Modules;

/// <summary>Informação rápida mostrada na tela inicial da ilha (ex.: tempo agora, próximo compromisso).</summary>
/// <param name="Order">Posição relativa (menor primeiro).</param>
/// <param name="Invoke">Ação ao clicar (ex.: abrir o painel do clima); <c>null</c> = só informativo.</param>
/// <param name="ShortText">Versão curta para a ilha compacta em repouso; <c>null</c> usa <paramref name="Text"/>.</param>
/// <param name="IdleOnly">Só para a ilha em repouso: não aparece na lista da tela inicial.</param>
public sealed record IslandGlance(string Id, string Glyph, string Text, int Order = 0, Action? Invoke = null, string? ShortText = null, bool IdleOnly = false);

/// <summary>Módulos publicam e retiram informações rápidas; o relógio as exibe abaixo da data.</summary>
public interface IGlanceRegistry
{
    IReadOnlyList<IslandGlance> Glances { get; }

    /// <summary>Pode ser disparado em qualquer thread.</summary>
    event EventHandler? Changed;

    /// <summary>Inclui ou substitui (pelo Id).</summary>
    void Set(IslandGlance glance);

    void Remove(string id);
}

public sealed class GlanceRegistry : IGlanceRegistry
{
    private readonly Lock _lock = new();
    private IReadOnlyList<IslandGlance> _glances = [];

    public IReadOnlyList<IslandGlance> Glances
    {
        get
        {
            lock (_lock)
            {
                return _glances;
            }
        }
    }

    public event EventHandler? Changed;

    public void Set(IslandGlance glance)
    {
        lock (_lock)
        {
            if (_glances.Contains(glance))
            {
                return;
            }

            _glances = [.. _glances.Where(g => g.Id != glance.Id).Append(glance).OrderBy(g => g.Order)];
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Remove(string id)
    {
        lock (_lock)
        {
            if (_glances.All(g => g.Id != id))
            {
                return;
            }

            _glances = [.. _glances.Where(g => g.Id != id)];
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }
}
