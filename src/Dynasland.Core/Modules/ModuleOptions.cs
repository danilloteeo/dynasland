using System.Globalization;
using Dynasland.Core.Interfaces;

namespace Dynasland.Core.Modules;

public enum ModuleOptionKind
{
    Toggle,
    Slider,
}

/// <summary>
/// Preferência de um módulo, exibida automaticamente na janela de configurações.
/// Para <see cref="ModuleOptionKind.Toggle"/>, valores 1 (ligado) e 0 (desligado).
/// </summary>
public sealed record ModuleOption(
    string Key,
    string Label,
    string Description,
    ModuleOptionKind Kind,
    double DefaultValue,
    double Minimum = 0,
    double Maximum = 1,
    double Step = 1,
    string Unit = "")
{
    public static ModuleOption Toggle(string key, string label, string description, bool defaultValue = true) =>
        new(key, label, description, ModuleOptionKind.Toggle, defaultValue ? 1 : 0);

    public static ModuleOption Slider(string key, string label, string description, double defaultValue, double minimum, double maximum, double step, string unit) =>
        new(key, label, description, ModuleOptionKind.Slider, defaultValue, minimum, maximum, step, unit);

    public double Normalize(double value) => Kind == ModuleOptionKind.Toggle
        ? value >= 0.5 ? 1 : 0
        : Math.Clamp(Math.Round(value / Step) * Step, Minimum, Maximum);
}

public interface IModuleOptions
{
    /// <summary>Disparado (em qualquer thread) com (módulo, chave) alterados.</summary>
    event EventHandler<(string ModuleId, string Key)>? Changed;

    Task LoadAsync(CancellationToken cancellationToken = default);

    double Get(string moduleId, ModuleOption option);

    bool IsOn(string moduleId, ModuleOption option);

    Task SetAsync(string moduleId, ModuleOption option, double value, CancellationToken cancellationToken = default);
}

/// <summary>Valores das opções dos módulos, guardados nas configurações como <c>module.{id}.{chave}</c>.</summary>
public sealed class ModuleOptionsService(ISettingsStore store) : IModuleOptions
{
    private readonly Lock _lock = new();
    private Dictionary<string, double> _values = [];

    public event EventHandler<(string ModuleId, string Key)>? Changed;

    public static string StorageKey(string moduleId, string key) => $"module.{moduleId}.{key}";

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        var all = await store.GetAllAsync(cancellationToken);
        var values = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (var (key, raw) in all)
        {
            if (key.StartsWith("module.", StringComparison.Ordinal)
                && double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
                && double.IsFinite(value))
            {
                values[key] = value;
            }
        }

        lock (_lock)
        {
            _values = values;
        }
    }

    public double Get(string moduleId, ModuleOption option)
    {
        lock (_lock)
        {
            return _values.TryGetValue(StorageKey(moduleId, option.Key), out var value)
                ? option.Normalize(value)
                : option.DefaultValue;
        }
    }

    public bool IsOn(string moduleId, ModuleOption option) => Get(moduleId, option) >= 0.5;

    public async Task SetAsync(string moduleId, ModuleOption option, double value, CancellationToken cancellationToken = default)
    {
        var normalized = option.Normalize(value);
        var key = StorageKey(moduleId, option.Key);
        lock (_lock)
        {
            if (_values.TryGetValue(key, out var current) && current == normalized)
            {
                return;
            }

            _values[key] = normalized;
        }

        await store.SetManyAsync(new Dictionary<string, string> { [key] = normalized.ToString(CultureInfo.InvariantCulture) }, cancellationToken);
        Changed?.Invoke(this, (moduleId, option.Key));
    }
}
