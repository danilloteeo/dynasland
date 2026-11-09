using Dynasland.Core.Layout;
using Dynasland.Core.Models;

namespace Dynasland.Core.Activities;

/// <summary>
/// Máquina de estados da ilha: decide o que exibir a partir das atividades publicadas
/// pelos módulos e das interações do usuário.
/// </summary>
/// <remarks>
/// Regras:
/// <list type="bullet">
/// <item>Atividades transitórias (e contínuas com <see cref="IslandActivity.ExpandOnStart"/>) expandem a ilha
/// pelo tempo configurado; as que chegam enquanto outra está visível entram numa fila por prioridade.</item>
/// <item>Uma atividade de prioridade maior substitui a atual; a substituída volta para a fila.</item>
/// <item>Com a ilha interativa, só prioridade <see cref="ActivityPriority.High"/> ou maior interrompe o usuário.</item>
/// <item>Com o ponteiro sobre a ilha, nada expira.</item>
/// </list>
/// Não é thread-safe: use sempre da mesma thread. O tempo é recebido como parâmetro e
/// quem hospeda deve chamar <see cref="Advance"/> quando <see cref="NextDeadline"/> vencer.
/// </remarks>
public sealed class IslandCoordinator(Func<IslandSettings> settings)
{
    private static readonly TimeSpan HoverGrace = TimeSpan.FromSeconds(2);

    private readonly Dictionary<string, Entry> _live = new(StringComparer.Ordinal);
    private readonly List<Entry> _queue = [];
    private long _sequence;

    private IslandState _state = IslandState.Compact;
    private Entry? _shown;
    private DateTimeOffset _shownUntil;
    private string? _focusId;
    private DateTimeOffset? _collapseAt;
    private bool _pointerOver;

    public IslandSnapshot Snapshot { get; private set; } = IslandSnapshot.Empty;

    public event EventHandler<IslandSnapshot>? Changed;

    /// <summary>Próximo instante em que <see cref="Advance"/> precisa ser chamado; <c>null</c> se nenhum.</summary>
    public DateTimeOffset? NextDeadline => _pointerOver
        ? null
        : _state switch
        {
            IslandState.Expanded => _shownUntil,
            IslandState.Interactive => _collapseAt,
            _ => null,
        };

    private TimeSpan ExpandedDuration => TimeSpan.FromSeconds(settings().ExpandedDurationSeconds);

    public void Post(IslandActivity activity, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(activity);

        if (activity.Kind == ActivityKind.Live)
        {
            var isNew = !_live.TryGetValue(activity.Id, out var existing);
            var entry = new Entry(activity, isNew ? ++_sequence : existing!.Sequence);
            _live[activity.Id] = entry;
            ReplaceQueued(entry);
            if (_shown?.Id == activity.Id)
            {
                _shown = entry;
            }
            else if (isNew && activity.ExpandOnStart)
            {
                Announce(entry, now);
            }
        }
        else if (_shown?.Id == activity.Id)
        {
            // Mesma atividade já visível (ex.: volume mudando de novo): atualiza e reinicia o tempo.
            _shown = new Entry(activity, _shown.Sequence);
            _shownUntil = now + DurationOf(activity);
        }
        else if (!ReplaceQueued(new Entry(activity, 0)))
        {
            Announce(new Entry(activity, ++_sequence), now);
        }

        Publish();
    }

    public void Remove(string activityId, DateTimeOffset now)
    {
        RemoveCore(activityId, now);
        Publish();
    }

    /// <summary>Remove todas as atividades de um módulo (ex.: módulo desativado).</summary>
    public void RemoveModule(string moduleId, DateTimeOffset now)
    {
        var ids = _live.Values.Select(e => e.Activity)
            .Concat(_queue.Select(e => e.Activity))
            .Append(_shown?.Activity)
            .Where(a => a is not null && a.ModuleId == moduleId)
            .Select(a => a!.Id)
            .Distinct()
            .ToList();

        foreach (var id in ids)
        {
            RemoveCore(id, now);
        }

        Publish();
    }

    /// <summary>Clique na ilha: abre ou fecha o estado interativo.</summary>
    public void ToggleInteractive(DateTimeOffset now)
    {
        if (_state == IslandState.Interactive)
        {
            LeaveInteractive();
            ShowNextOrCompact(now);
            Publish();
            return;
        }

        var focus = _shown?.Id ?? TopLive()?.Id;
        if (focus is null)
        {
            return;
        }

        _focusId = focus;
        _state = IslandState.Interactive;
        _collapseAt = _pointerOver ? null : now + ExpandedDuration;
        Publish();
    }

    /// <summary>Alterna entre as atividades disponíveis no estado interativo.</summary>
    public void Cycle(int direction, DateTimeOffset now)
    {
        if (_state != IslandState.Interactive || direction == 0)
        {
            return;
        }

        var candidates = OrderedLive().Select(e => e.Id).ToList();
        if (_shown is { Activity.Kind: ActivityKind.Transient })
        {
            candidates.Insert(0, _shown.Id);
        }

        if (candidates.Count < 2)
        {
            return;
        }

        var index = Math.Max(0, candidates.IndexOf(_focusId!));
        _focusId = candidates[(index + Math.Sign(direction) + candidates.Count) % candidates.Count];
        _collapseAt = _pointerOver ? null : now + ExpandedDuration;
        Publish();
    }

    /// <summary>Volta ao compacto e descarta transitórias pendentes (ex.: ilha ocultada).</summary>
    public void Reset()
    {
        LeaveInteractive();
        _queue.Clear();
        _shown = null;
        _state = IslandState.Compact;
        Publish();
    }

    public void PointerEntered() => _pointerOver = true;

    public void PointerExited(DateTimeOffset now)
    {
        _pointerOver = false;
        if (_state == IslandState.Interactive)
        {
            _collapseAt = now + ExpandedDuration;
        }
        else if (_state == IslandState.Expanded && _shownUntil < now + HoverGrace)
        {
            _shownUntil = now + HoverGrace;
        }
    }

    /// <summary>Processa os prazos vencidos até <paramref name="now"/>.</summary>
    public void Advance(DateTimeOffset now)
    {
        if (_pointerOver)
        {
            return;
        }

        if (_state == IslandState.Expanded && now >= _shownUntil)
        {
            ShowNextOrCompact(now);
        }
        else if (_state == IslandState.Interactive && _collapseAt is { } collapseAt && now >= collapseAt)
        {
            LeaveInteractive();
            ShowNextOrCompact(now);
        }

        Publish();
    }

    private void Announce(Entry entry, DateTimeOffset now)
    {
        if (_state == IslandState.Interactive)
        {
            if (entry.Priority < ActivityPriority.High)
            {
                _queue.Add(entry);
                return;
            }

            LeaveInteractive();
        }

        if (_shown is null)
        {
            Show(entry, now);
        }
        else if (entry.Priority > _shown.Priority)
        {
            if (_shown.Activity.Kind == ActivityKind.Transient)
            {
                _queue.Add(_shown);
            }

            Show(entry, now);
        }
        else
        {
            _queue.Add(entry);
        }
    }

    private void Show(Entry entry, DateTimeOffset now)
    {
        _shown = entry;
        _shownUntil = now + DurationOf(entry.Activity);
        _state = IslandState.Expanded;
    }

    private void ShowNextOrCompact(DateTimeOffset now)
    {
        var next = _queue
            .OrderByDescending(e => e.Priority)
            .ThenBy(e => e.Sequence)
            .FirstOrDefault();

        if (next is null)
        {
            _shown = null;
            _state = IslandState.Compact;
            return;
        }

        _queue.Remove(next);
        Show(next, now);
    }

    private void LeaveInteractive()
    {
        if (_state != IslandState.Interactive)
        {
            return;
        }

        _focusId = null;
        _collapseAt = null;
        _shown = null;
        _state = IslandState.Compact;
    }

    private void RemoveCore(string activityId, DateTimeOffset now)
    {
        _live.Remove(activityId);
        _queue.RemoveAll(e => e.Id == activityId);

        if (_state == IslandState.Expanded && _shown?.Id == activityId)
        {
            ShowNextOrCompact(now);
        }
        else if (_state == IslandState.Interactive && _focusId == activityId)
        {
            if (_shown?.Id == activityId)
            {
                _shown = null;
            }

            var top = TopLive();
            if (top is null)
            {
                LeaveInteractive();
                ShowNextOrCompact(now);
            }
            else
            {
                _focusId = top.Id;
            }
        }
        else if (_shown?.Id == activityId)
        {
            _shown = null;
        }
    }

    /// <summary>Substitui na fila mantendo a posição original.</summary>
    private bool ReplaceQueued(Entry entry)
    {
        var index = _queue.FindIndex(e => e.Id == entry.Id);
        if (index < 0)
        {
            return false;
        }

        _queue[index] = entry with { Sequence = _queue[index].Sequence };
        return true;
    }

    private TimeSpan DurationOf(IslandActivity activity) => activity.Duration ?? ExpandedDuration;

    private IEnumerable<Entry> OrderedLive() => _live.Values
        .OrderByDescending(e => e.Priority)
        .ThenByDescending(e => e.Sequence);

    private Entry? TopLive() => OrderedLive().FirstOrDefault();

    private IslandActivity? Focused => _state switch
    {
        IslandState.Expanded => _shown?.Activity,
        IslandState.Interactive when _focusId is not null =>
            _shown?.Id == _focusId ? _shown.Activity : _live.GetValueOrDefault(_focusId)?.Activity,
        _ => null,
    };

    private void Publish()
    {
        var focused = Focused;
        var indicator = TopLive()?.Activity;
        var content = _state == IslandState.Compact ? indicator?.Content : focused?.Content;
        var dimensions = IslandLayout.Clamp(content?.GetPreferredDimensions(_state) ?? IslandLayout.For(_state));

        var snapshot = new IslandSnapshot(_state, focused, indicator, dimensions, _live.Count);
        if (snapshot == Snapshot)
        {
            return;
        }

        Snapshot = snapshot;
        Changed?.Invoke(this, snapshot);
    }

    private sealed record Entry(IslandActivity Activity, long Sequence)
    {
        public string Id => Activity.Id;

        public ActivityPriority Priority => Activity.Priority;
    }
}
