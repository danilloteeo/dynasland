using Dynasland.Core.Interfaces;

namespace Dynasland.Core.Shortcuts;

/// <summary>Ação que pode receber um atalho de teclado global (funciona com qualquer programa em foco).</summary>
/// <param name="DefaultGesture">Atalho de fábrica; <c>null</c> = sem atalho até o usuário definir.</param>
/// <param name="Invoke">Executado na thread de UI.</param>
public sealed record GlobalShortcut(string Id, string Label, string Category, HotkeyGesture? DefaultGesture, Action Invoke);

/// <summary>Estado de um atalho global: combinação efetiva e se o Windows aceitou registrá-la.</summary>
public sealed record GlobalShortcutBinding(GlobalShortcut Shortcut, HotkeyGesture? Gesture, bool IsCustom, string? Error);

/// <summary>
/// Catálogo de ações com atalho global. O app e os módulos registram ações enquanto estão ativos;
/// o serviço de teclado da interface registra as combinações no Windows.
/// As combinações escolhidas pelo usuário ficam nas configurações como <c>hotkey.{id}</c> (vazio = desligado).
/// </summary>
public interface IGlobalShortcutRegistry
{
    IReadOnlyList<GlobalShortcutBinding> Bindings { get; }

    /// <summary>Catálogo ou combinações mudaram. Pode ser disparado em qualquer thread.</summary>
    event EventHandler? Changed;

    Task LoadAsync(CancellationToken cancellationToken = default);

    IDisposable Register(GlobalShortcut shortcut);

    /// <summary>Define a combinação (<c>null</c> desliga o atalho). Tira a combinação de outra ação que a usava.</summary>
    Task SetGestureAsync(string shortcutId, HotkeyGesture? gesture, CancellationToken cancellationToken = default);

    Task ResetAsync(string shortcutId, CancellationToken cancellationToken = default);

    /// <summary>Informado pelo serviço de teclado: a combinação não pôde ser registrada (em uso por outro programa).</summary>
    void ReportRegistration(string shortcutId, string? error);
}

public sealed class GlobalShortcutRegistry(ISettingsStore store) : IGlobalShortcutRegistry
{
    public const string KeyPrefix = "hotkey.";

    private readonly Lock _lock = new();
    private readonly List<GlobalShortcut> _shortcuts = [];
    private readonly Dictionary<string, string> _overrides = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string?> _errors = new(StringComparer.Ordinal);

    public event EventHandler? Changed;

    public IReadOnlyList<GlobalShortcutBinding> Bindings
    {
        get
        {
            lock (_lock)
            {
                return [.. _shortcuts.Select(Bind)];
            }
        }
    }

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        var values = await store.GetAllAsync(cancellationToken);
        lock (_lock)
        {
            _overrides.Clear();
            foreach (var (key, value) in values.Where(p => p.Key.StartsWith(KeyPrefix, StringComparison.Ordinal)))
            {
                _overrides[key[KeyPrefix.Length..]] = value;
            }
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    public IDisposable Register(GlobalShortcut shortcut)
    {
        lock (_lock)
        {
            _shortcuts.RemoveAll(s => s.Id == shortcut.Id);
            _shortcuts.Add(shortcut);
        }

        Changed?.Invoke(this, EventArgs.Empty);
        return new Registration(this, shortcut);
    }

    public async Task SetGestureAsync(string shortcutId, HotkeyGesture? gesture, CancellationToken cancellationToken = default)
    {
        Dictionary<string, string> changes = [];
        lock (_lock)
        {
            if (gesture is not null)
            {
                foreach (var other in _shortcuts.Where(s => s.Id != shortcutId && Bind(s).Gesture == gesture).ToList())
                {
                    _overrides[other.Id] = string.Empty;
                    changes[KeyPrefix + other.Id] = string.Empty;
                }
            }

            var value = gesture?.ToString() ?? string.Empty;
            _overrides[shortcutId] = value;
            changes[KeyPrefix + shortcutId] = value;
        }

        await store.SetManyAsync(changes, cancellationToken);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public async Task ResetAsync(string shortcutId, CancellationToken cancellationToken = default)
    {
        GlobalShortcut? shortcut;
        lock (_lock)
        {
            shortcut = _shortcuts.FirstOrDefault(s => s.Id == shortcutId);
        }

        await SetGestureAsync(shortcutId, shortcut?.DefaultGesture, cancellationToken);
    }

    public void ReportRegistration(string shortcutId, string? error)
    {
        lock (_lock)
        {
            if (_errors.GetValueOrDefault(shortcutId) == error)
            {
                return;
            }

            _errors[shortcutId] = error;
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    private GlobalShortcutBinding Bind(GlobalShortcut shortcut)
    {
        var custom = _overrides.TryGetValue(shortcut.Id, out var value);
        var gesture = custom ? HotkeyGesture.TryParse(value) : shortcut.DefaultGesture;
        return new GlobalShortcutBinding(shortcut, gesture, custom && gesture != shortcut.DefaultGesture, _errors.GetValueOrDefault(shortcut.Id));
    }

    private void Unregister(GlobalShortcut shortcut)
    {
        lock (_lock)
        {
            if (!_shortcuts.Remove(shortcut))
            {
                return;
            }
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    private sealed class Registration(GlobalShortcutRegistry owner, GlobalShortcut shortcut) : IDisposable
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
