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

/// <summary>O que acontece com as notificações de um aplicativo.</summary>
public enum NotificationVisibility
{
    /// <summary>Aparecem na ilha e ficam no histórico.</summary>
    Show,

    /// <summary>Não aparecem na ilha, mas ficam no histórico da Central.</summary>
    HistoryOnly,

    /// <summary>Ignoradas: nem ilha nem histórico.</summary>
    Block,
}

/// <summary>Como tratar uma notificação recebida, já combinando as regras do app com as gerais.</summary>
public sealed record NotificationDecision(NotificationVisibility Visibility, bool HighPriority, TimeSpan Duration);

/// <param name="Rule"><c>true</c> permitido, <c>false</c> bloqueado, <c>null</c> segue o modo.</param>
/// <param name="HistoryOnly">Permitido, mas só no histórico (não aparece na ilha).</param>
/// <param name="HighPriority">Interrompe a ilha mesmo durante o uso.</param>
/// <param name="DurationSeconds">Tempo na ilha só deste app; <c>null</c> usa o geral.</param>
public sealed record KnownNotificationApp(string AppId, string Name, bool? Rule, bool HistoryOnly = false, bool HighPriority = false, double? DurationSeconds = null);

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
    public const string HistoryOnlyKey = "notifications.appHistoryOnly";
    public const string PriorityKey = "notifications.appPriority";
    public const string DurationsKey = "notifications.appDurations";

    /// <summary>Formato da Fase 3 (somente bloqueados); migrado para <see cref="RulesKey"/> ao carregar.</summary>
    public const string LegacyBlockedAppsKey = "notifications.blockedApps";

    public const double DefaultDurationSeconds = 6;

    private readonly Lock _lock = new();
    private Dictionary<string, bool> _rules = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, string> _knownApps = new(StringComparer.OrdinalIgnoreCase);
    private HashSet<string> _historyOnly = new(StringComparer.OrdinalIgnoreCase);
    private HashSet<string> _highPriority = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, double> _durations = new(StringComparer.OrdinalIgnoreCase);

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
                    .Select(id => new KnownNotificationApp(
                        id,
                        _knownApps.GetValueOrDefault(id, id),
                        _rules.TryGetValue(id, out var rule) ? rule : null,
                        _historyOnly.Contains(id),
                        _highPriority.Contains(id),
                        _durations.TryGetValue(id, out var seconds) ? seconds : null))
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
            _historyOnly = new HashSet<string>(Lines(values.GetValueOrDefault(HistoryOnlyKey)), StringComparer.OrdinalIgnoreCase);
            _highPriority = new HashSet<string>(Lines(values.GetValueOrDefault(PriorityKey)), StringComparer.OrdinalIgnoreCase);
            _durations = ParsePairs(values.GetValueOrDefault(DurationsKey))
                .Where(p => double.TryParse(p.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
                .ToDictionary(p => p.Key, p => Math.Clamp(double.Parse(p.Value, CultureInfo.InvariantCulture), 2, 30), StringComparer.OrdinalIgnoreCase);
        }
    }

    /// <summary>Decide visibilidade, prioridade e duração de uma notificação do aplicativo.</summary>
    public NotificationDecision Decide(string appId)
    {
        lock (_lock)
        {
            var allowed = _rules.TryGetValue(appId, out var rule) ? rule : Mode == NotificationFilterMode.AllExceptBlocked;
            var visibility = !allowed
                ? NotificationVisibility.Block
                : _historyOnly.Contains(appId) ? NotificationVisibility.HistoryOnly : NotificationVisibility.Show;
            var seconds = _durations.TryGetValue(appId, out var custom) ? custom : DurationSeconds;
            return new NotificationDecision(visibility, CanInterrupt || _highPriority.Contains(appId), TimeSpan.FromSeconds(seconds));
        }
    }

    public Task SetHistoryOnlyAsync(string appId, bool historyOnly, CancellationToken cancellationToken = default) =>
        SetFlagAsync(_historyOnly, HistoryOnlyKey, appId, historyOnly, cancellationToken);

    public Task SetHighPriorityAsync(string appId, bool highPriority, CancellationToken cancellationToken = default) =>
        SetFlagAsync(_highPriority, PriorityKey, appId, highPriority, cancellationToken);

    /// <param name="seconds"><c>null</c> volta a usar a duração geral.</param>
    public Task SetAppDurationAsync(string appId, double? seconds, CancellationToken cancellationToken = default)
    {
        string serialized;
        lock (_lock)
        {
            if (seconds is { } value)
            {
                _durations[appId] = Math.Clamp(Math.Round(value), 2, 30);
            }
            else
            {
                _durations.Remove(appId);
            }

            serialized = SerializePairs(_durations.Select(d => (d.Key, d.Value.ToString(CultureInfo.InvariantCulture))));
        }

        return SaveAsync(new() { [DurationsKey] = serialized }, cancellationToken);
    }

    private Task SetFlagAsync(HashSet<string> set, string key, string appId, bool on, CancellationToken cancellationToken)
    {
        string serialized;
        lock (_lock)
        {
            if (!(on ? set.Add(appId) : set.Remove(appId)))
            {
                return Task.CompletedTask;
            }

            serialized = string.Join('\n', set.Order(StringComparer.OrdinalIgnoreCase));
        }

        return SaveAsync(new() { [key] = serialized }, cancellationToken);
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
