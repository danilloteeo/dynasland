using System.Globalization;
using Dynasland.Core.Interfaces;

namespace Dynasland.Modules.Notifications;

public enum NotificationFilterMode
{
    /// <summary>Mostra todos os aplicativos, exceto os bloqueados.</summary>
    AllExceptBlocked,

    /// <summary>Mostra apenas os aplicativos permitidos explicitamente.</summary>
    OnlyAllowed,
}

/// <param name="Rule"><c>true</c> permitido, <c>false</c> bloqueado, <c>null</c> segue o modo.</param>
public sealed record KnownNotificationApp(string AppId, string Name, bool? Rule);

/// <summary>
/// Preferências de notificações: quais aplicativos aparecem na ilha, por quanto tempo e se podem
/// interromper. Aplicativos que já enviaram notificações são lembrados para aparecerem nas configurações.
/// </summary>
public sealed class NotificationPreferences(ISettingsStore store)
{
    public const string ModeKey = "notifications.mode";
    public const string DurationKey = "notifications.durationSeconds";
    public const string InterruptKey = "notifications.interrupt";
    public const string RulesKey = "notifications.rules";
    public const string KnownAppsKey = "notifications.knownApps";

    /// <summary>Formato da Fase 3 (somente bloqueados); migrado para <see cref="RulesKey"/> ao carregar.</summary>
    public const string LegacyBlockedAppsKey = "notifications.blockedApps";

    public const double DefaultDurationSeconds = 6;

    private readonly Lock _lock = new();
    private Dictionary<string, bool> _rules = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, string> _knownApps = new(StringComparer.OrdinalIgnoreCase);

    public event EventHandler? Changed;

    public NotificationFilterMode Mode { get; private set; }

    public double DurationSeconds { get; private set; } = DefaultDurationSeconds;

    /// <summary>Notificações com prioridade alta interrompem a ilha mesmo durante o uso.</summary>
    public bool CanInterrupt { get; private set; }

    public IReadOnlyList<KnownNotificationApp> KnownApps
    {
        get
        {
            lock (_lock)
            {
                return _knownApps.Keys
                    .Union(_rules.Keys, StringComparer.OrdinalIgnoreCase)
                    .Select(id => new KnownNotificationApp(id, _knownApps.GetValueOrDefault(id, id), _rules.TryGetValue(id, out var rule) ? rule : null))
                    .OrderBy(app => app.Name, StringComparer.CurrentCultureIgnoreCase)
                    .ToList();
            }
        }
    }

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        var values = await store.GetAllAsync(cancellationToken);

        var rules = ParsePairs(values.GetValueOrDefault(RulesKey))
            .ToDictionary(p => p.Key, p => p.Value == "1", StringComparer.OrdinalIgnoreCase);
        foreach (var legacy in Lines(values.GetValueOrDefault(LegacyBlockedAppsKey)))
        {
            rules.TryAdd(legacy, false);
        }

        lock (_lock)
        {
            _rules = rules;
            _knownApps = ParsePairs(values.GetValueOrDefault(KnownAppsKey)).ToDictionary(p => p.Key, p => p.Value, StringComparer.OrdinalIgnoreCase);
            Mode = Enum.TryParse<NotificationFilterMode>(values.GetValueOrDefault(ModeKey), out var mode) && Enum.IsDefined(mode) ? mode : NotificationFilterMode.AllExceptBlocked;
            DurationSeconds = double.TryParse(values.GetValueOrDefault(DurationKey), NumberStyles.Float, CultureInfo.InvariantCulture, out var duration)
                ? Math.Clamp(duration, 2, 30)
                : DefaultDurationSeconds;
            CanInterrupt = values.GetValueOrDefault(InterruptKey) == "true";
        }
    }

    public bool ShouldShow(string appId)
    {
        lock (_lock)
        {
            return _rules.TryGetValue(appId, out var rule) ? rule : Mode == NotificationFilterMode.AllExceptBlocked;
        }
    }

    public bool IsBlocked(string appId) => !ShouldShow(appId);

    public Task SetBlockedAsync(string appId, bool blocked, CancellationToken cancellationToken = default) =>
        SetRuleAsync(appId, !blocked, cancellationToken);

    public async Task SetRuleAsync(string appId, bool show, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(appId))
        {
            return;
        }

        string serialized;
        lock (_lock)
        {
            if (_rules.TryGetValue(appId, out var current) && current == show)
            {
                return;
            }

            _rules[appId] = show;
            serialized = SerializePairs(_rules.Select(r => (r.Key, r.Value ? "1" : "0")));
        }

        await SaveAsync(new() { [RulesKey] = serialized, [LegacyBlockedAppsKey] = string.Empty }, cancellationToken);
    }

    /// <summary>Lembra um aplicativo que enviou notificação, para listá-lo nas configurações.</summary>
    public async Task RememberAppAsync(string appId, string name, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(appId))
        {
            return;
        }

        string serialized;
        lock (_lock)
        {
            if (_knownApps.TryGetValue(appId, out var known) && known == name)
            {
                return;
            }

            _knownApps[appId] = name;
            serialized = SerializePairs(_knownApps.Select(a => (a.Key, a.Value)));
        }

        await SaveAsync(new() { [KnownAppsKey] = serialized }, cancellationToken);
    }

    public Task SetModeAsync(NotificationFilterMode mode, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            Mode = mode;
        }

        return SaveAsync(new() { [ModeKey] = mode.ToString() }, cancellationToken);
    }

    public Task SetDurationAsync(double seconds, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            DurationSeconds = Math.Clamp(Math.Round(seconds), 2, 30);
        }

        return SaveAsync(new() { [DurationKey] = DurationSeconds.ToString(CultureInfo.InvariantCulture) }, cancellationToken);
    }

    public Task SetCanInterruptAsync(bool canInterrupt, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            CanInterrupt = canInterrupt;
        }

        return SaveAsync(new() { [InterruptKey] = canInterrupt ? "true" : "false" }, cancellationToken);
    }

    private async Task SaveAsync(Dictionary<string, string> values, CancellationToken cancellationToken)
    {
        await store.SetManyAsync(values, cancellationToken);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private static IEnumerable<string> Lines(string? raw) =>
        string.IsNullOrEmpty(raw) ? [] : raw.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static IEnumerable<KeyValuePair<string, string>> ParsePairs(string? raw) => Lines(raw)
        .Select(line => line.Split('\t', 2))
        .Where(parts => parts.Length == 2 && parts[0].Length > 0)
        .Select(parts => new KeyValuePair<string, string>(parts[0], parts[1]));

    private static string SerializePairs(IEnumerable<(string Key, string Value)> pairs) =>
        string.Join('\n', pairs
            .OrderBy(p => p.Key, StringComparer.OrdinalIgnoreCase)
            .Select(p => $"{p.Key.Replace('\t', ' ').Replace('\n', ' ')}\t{p.Value.Replace('\t', ' ').Replace('\n', ' ')}"));
}
