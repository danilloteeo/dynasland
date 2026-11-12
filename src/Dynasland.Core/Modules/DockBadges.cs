namespace Dynasland.Core.Modules;

public enum DockBadgeKind
{
    /// <summary>Ponto de aviso (ex.: notificações não vistas).</summary>
    Dot,

    /// <summary>Anel fino de progresso em volta do ícone (ex.: água do dia).</summary>
    Ring,
}

/// <param name="Progress">De 0 a 1; só vale para <see cref="DockBadgeKind.Ring"/>.</param>
public sealed record DockBadge(DockBadgeKind Kind, double Progress = 0);

/// <summary>Selos que os módulos põem no ícone deles na dock.</summary>
public interface IDockBadges
{
    IReadOnlyDictionary<string, DockBadge> Badges { get; }

    /// <summary>Pode ser disparado em qualquer thread; só quando algo muda de fato.</summary>
    event EventHandler? Changed;

    void Set(string moduleId, DockBadge badge);

    void Clear(string moduleId);
}

public sealed class DockBadgeRegistry : IDockBadges
{
    private readonly Lock _lock = new();
    private IReadOnlyDictionary<string, DockBadge> _badges = new Dictionary<string, DockBadge>();

    public IReadOnlyDictionary<string, DockBadge> Badges
    {
        get
        {
            lock (_lock)
            {
                return _badges;
            }
        }
    }

    public event EventHandler? Changed;

    public void Set(string moduleId, DockBadge badge)
    {
        badge = badge with { Progress = Math.Clamp(badge.Progress, 0, 1) };
        lock (_lock)
        {
            if (_badges.TryGetValue(moduleId, out var current) && current == badge)
            {
                return;
            }

            _badges = new Dictionary<string, DockBadge>(_badges) { [moduleId] = badge };
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Clear(string moduleId)
    {
        lock (_lock)
        {
            if (!_badges.ContainsKey(moduleId))
            {
                return;
            }

            var copy = new Dictionary<string, DockBadge>(_badges);
            copy.Remove(moduleId);
            _badges = copy;
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }
}

/// <summary>Módulos que o usuário escondeu da dock (lista de ids separada por vírgula nas configurações).</summary>
public static class DockVisibility
{
    public static IReadOnlySet<string> Parse(string? hidden) =>
        (hidden ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.Ordinal);

    /// <summary>A lista depois de mostrar ou esconder <paramref name="moduleId"/>; em ordem alfabética, sem repetição.</summary>
    public static string With(string? hidden, string moduleId, bool visible)
    {
        var set = Parse(hidden).ToHashSet(StringComparer.Ordinal);
        if (visible)
        {
            set.Remove(moduleId);
        }
        else
        {
            set.Add(moduleId);
        }

        return string.Join(',', set.OrderBy(id => id, StringComparer.Ordinal));
    }
}
