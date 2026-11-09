using Dynasland.Core.Interfaces;

namespace Dynasland.Modules.Notifications;

/// <summary>Aplicativos cujas notificações não aparecem na ilha (persistido nas configurações).</summary>
public sealed class NotificationPreferences(ISettingsStore store)
{
    public const string BlockedAppsKey = "notifications.blockedApps";

    private readonly Lock _lock = new();
    private HashSet<string> _blocked = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyCollection<string> BlockedApps
    {
        get
        {
            lock (_lock)
            {
                return [.. _blocked];
            }
        }
    }

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        var values = await store.GetAllAsync(cancellationToken);
        var blocked = values.TryGetValue(BlockedAppsKey, out var raw)
            ? raw.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            : [];

        lock (_lock)
        {
            _blocked = new HashSet<string>(blocked, StringComparer.OrdinalIgnoreCase);
        }
    }

    public bool IsBlocked(string appId)
    {
        lock (_lock)
        {
            return _blocked.Contains(appId);
        }
    }

    public async Task SetBlockedAsync(string appId, bool blocked, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(appId))
        {
            return;
        }

        string serialized;
        lock (_lock)
        {
            var changed = blocked ? _blocked.Add(appId) : _blocked.Remove(appId);
            if (!changed)
            {
                return;
            }

            serialized = string.Join('\n', _blocked.Order(StringComparer.OrdinalIgnoreCase));
        }

        await store.SetManyAsync(new Dictionary<string, string> { [BlockedAppsKey] = serialized }, cancellationToken);
    }
}
