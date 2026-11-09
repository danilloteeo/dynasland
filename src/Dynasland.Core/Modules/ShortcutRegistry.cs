namespace Dynasland.Core.Modules;

/// <summary>Atalho exibido na tela inicial da ilha (ex.: iniciar cronômetro, abrir monitor do sistema).</summary>
/// <param name="Glyph">Ícone (Segoe Fluent Icons / MDL2).</param>
/// <param name="Invoke">Executado na thread de UI ao clicar.</param>
public sealed record IslandShortcut(string Id, string ModuleId, string Glyph, string Label, Action Invoke);

/// <summary>
/// Módulos registram atalhos enquanto estão ativos; a tela inicial mostra apenas os registrados.
/// Assim nenhum botão aparece para um módulo desativado.
/// </summary>
public interface IShortcutRegistry
{
    IReadOnlyList<IslandShortcut> Shortcuts { get; }

    /// <summary>Pode ser disparado em qualquer thread.</summary>
    event EventHandler? Changed;

    /// <returns>Descarte para remover o atalho.</returns>
    IDisposable Register(IslandShortcut shortcut);
}

public sealed class ShortcutRegistry : IShortcutRegistry
{
    private readonly Lock _lock = new();
    private IReadOnlyList<IslandShortcut> _shortcuts = [];

    public IReadOnlyList<IslandShortcut> Shortcuts
    {
        get
        {
            lock (_lock)
            {
                return _shortcuts;
            }
        }
    }

    public event EventHandler? Changed;

    public IDisposable Register(IslandShortcut shortcut)
    {
        lock (_lock)
        {
            _shortcuts = [.. _shortcuts.Where(s => s.Id != shortcut.Id), shortcut];
        }

        Changed?.Invoke(this, EventArgs.Empty);
        return new Registration(this, shortcut);
    }

    private void Unregister(IslandShortcut shortcut)
    {
        lock (_lock)
        {
            if (!_shortcuts.Contains(shortcut))
            {
                return;
            }

            _shortcuts = [.. _shortcuts.Where(s => s != shortcut)];
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    private sealed class Registration(ShortcutRegistry owner, IslandShortcut shortcut) : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                owner.Unregister(shortcut);
            }
        }
    }
}
