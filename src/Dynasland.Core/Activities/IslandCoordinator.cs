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
/// <item>Transitórias com o mesmo <see cref="IslandActivity.GroupKey"/> são agrupadas (a mais recente vale).</item>
/// <item>Com a ilha interativa, só prioridade <see cref="ActivityPriority.High"/> ou maior interrompe o usuário.</item>
/// <item>Com o ponteiro sobre a ilha, nada expira.</item>
/// <item>Com a ilha oculta (<see cref="SetHidden"/>), nada é exibido: as pendentes de prioridade alta esperam
/// a ilha voltar; as demais são descartadas e registradas como <see cref="ActivityRecordState.Dropped"/>.</item>
/// <item>O módulo fixado pelo usuário (<see cref="IslandSettings.PinnedModuleId"/>) é sempre o indicador do compacto.</item>
/// </list>
/// Todas as transições ficam em <see cref="History"/>.
/// Não é thread-safe: use sempre da mesma thread. O tempo é recebido como parâmetro e
/// quem hospeda deve chamar <see cref="Advance"/> quando <see cref="NextDeadline"/> vencer.
/// </remarks>
public sealed class IslandCoordinator(Func<IslandSettings> settings)
{
    public const int HistoryCapacity = 100;

    private readonly Dictionary<string, Entry> _live = new(StringComparer.Ordinal);
    private readonly HashSet<string> _dismissedLive = new(StringComparer.Ordinal);
    private readonly List<Entry> _queue = [];
    private readonly LinkedList<ActivityRecord> _history = new();
    private long _sequence;

    private IslandState _state = IslandState.Compact;
    private Entry? _shown;
    private DateTimeOffset _shownUntil;
    private string? _focusId;
    private DateTimeOffset? _collapseAt;
    private bool _pointerOver;
    private bool _hidden;

    public IslandSnapshot Snapshot { get; private set; } = IslandSnapshot.Empty;

    public event EventHandler<IslandSnapshot>? Changed;

    /// <summary>Próximo instante em que <see cref="Advance"/> precisa ser chamado; <c>null</c> se nenhum.</summary>
    public DateTimeOffset? NextDeadline => _pointerOver || _hidden
        ? null
        : _state switch
        {
            IslandState.Expanded => _shownUntil,
            IslandState.Interactive => _collapseAt,
            _ => null,
        };

    /// <summary>Atividades recentes, da mais nova para a mais antiga (no máximo <see cref="HistoryCapacity"/>).</summary>
    public IReadOnlyList<ActivityRecord> History => [.. _history];

    public bool IsHidden => _hidden;

    private TimeSpan ExpandedDuration => TimeSpan.FromSeconds(settings().ExpandedDurationSeconds);

    public void Post(IslandActivity activity, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(activity);

        if (activity.Kind == ActivityKind.Live)
        {
            var isNew = !_live.TryGetValue(activity.Id, out var existing);
            var entry = new Entry(activity, isNew ? ++_sequence : existing!.Sequence, now);
            _live[activity.Id] = entry;
            ReplaceQueued(entry);
            if (isNew)
            {
                Record(entry, ActivityRecordState.Shown, now);
            }

            if (_shown?.Id == activity.Id)
            {
                _shown = entry;
            }
            else if (isNew && activity.ExpandOnStart && !_dismissedLive.Contains(activity.Id))
            {
                Announce(entry, now);
            }
        }
        else if (activity.OpenInteractive)
        {
            _queue.RemoveAll(e => e.Id == activity.Id);
            _shown = new Entry(activity, ++_sequence, now);
            Record(_shown, ActivityRecordState.Shown, now);
            if (!_hidden)
            {
                _focusId = activity.Id;
                _state = IslandState.Interactive;
                _collapseAt = _pointerOver ? null : now + ExpandedDuration;
            }
        }
        else if (_shown?.Id == activity.Id)
        {
            // Mesma atividade já visível (ex.: volume mudando de novo): atualiza e reinicia o tempo.
            _shown = _shown with { Activity = activity };
            _shownUntil = now + DurationOf(activity);
        }
        else if (activity.GroupKey is { } group && _shown is { } shown && shown.Activity.GroupKey == group && _state == IslandState.Expanded)
        {
            Record(shown, ActivityRecordState.Grouped, now);
            _shown = new Entry(activity, shown.Sequence, now) { GroupCount = shown.GroupCount + 1 };
            _shownUntil = now + DurationOf(activity);
            Record(_shown, ActivityRecordState.Shown, now);
        }
        else if (activity.GroupKey is { } key && _queue.FindIndex(e => e.Activity.GroupKey == key) is var groupIndex and >= 0)
        {
            var previous = _queue[groupIndex];
            Record(previous, ActivityRecordState.Grouped, now);
            _queue[groupIndex] = new Entry(activity, previous.Sequence, now) { GroupCount = previous.GroupCount + 1 };
            Record(_queue[groupIndex], ActivityRecordState.Queued, now);
        }
        else if (!ReplaceQueued(new Entry(activity, 0, now)))
        {
            Announce(new Entry(activity, ++_sequence, now), now);
        }

        Publish();
    }

    public void Remove(string activityId, DateTimeOffset now)
    {
        RemoveCore(activityId, now, ActivityRecordState.Removed);
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
            RemoveCore(id, now, ActivityRecordState.Removed);
        }

        Publish();
    }

    /// <summary>
    /// O usuário fechou a atividade em foco. Transitórias saem; contínuas ficam ocultas até o módulo
    /// removê-las (uma nova publicação com o mesmo Id continua oculta).
    /// </summary>
    /// <returns>A atividade fechada, para avisar o módulo; <c>null</c> se nada pôde ser fechado.</returns>
    public IslandActivity? DismissFocused(DateTimeOffset now)
    {
        if (Focused is not { CanDismiss: true } focused)
        {
            return null;
        }

        if (focused.Kind == ActivityKind.Live)
        {
            _dismissedLive.Add(focused.Id);
        }

        RemoveCore(focused.Id, now, ActivityRecordState.Dismissed, keepLive: focused.Kind == ActivityKind.Live);
        Publish();
        return focused;
    }

    /// <summary>Clique na ilha: abre ou fecha o estado interativo.</summary>
    public void ToggleInteractive(DateTimeOffset now)
    {
        if (_state == IslandState.Interactive)
        {
            CollapseInteractive(now);
            return;
        }

        var focus = _shown?.Id ?? TopLive()?.Id;
        if (focus is null || _hidden)
        {
            return;
        }

        _focusId = focus;
        _state = IslandState.Interactive;
        _collapseAt = _pointerOver ? null : now + ExpandedDuration;
        Publish();
    }

    /// <summary>Fecha o estado interativo (ex.: clique fora da ilha); sem efeito nos outros estados.</summary>
    public void CollapseInteractive(DateTimeOffset now)
    {
        if (_state != IslandState.Interactive)
        {
            return;
        }

        LeaveInteractive(now);
        ShowNextOrCompact(now);
        Publish();
    }

    /// <summary>Alterna entre as atividades disponíveis no estado interativo.</summary>
    public void Cycle(int direction, DateTimeOffset now)
    {
        if (_state != IslandState.Interactive || direction == 0)
        {
            return;
        }

        var pages = InteractivePages();
        if (pages.Count < 2)
        {
            return;
        }

        var index = Math.Max(0, pages.IndexOf(_focusId!));
        SelectPage((index + Math.Sign(direction) + pages.Count) % pages.Count, now);
    }

    /// <summary>Vai direto para uma aba do estado interativo (ex.: clique no indicador de página).</summary>
    public void SelectPage(int index, DateTimeOffset now)
    {
        if (_state != IslandState.Interactive)
        {
            return;
        }

        var pages = InteractivePages();
        if (index < 0 || index >= pages.Count)
        {
            return;
        }

        _focusId = pages[index];
        _collapseAt = _pointerOver ? null : now + ExpandedDuration;
        Publish();
    }

    /// <summary>
    /// A janela da ilha foi ocultada (tela cheia, bloqueio, usuário) ou voltou. Oculta, a ilha volta ao
    /// compacto; transitórias de prioridade alta esperam, as demais são descartadas com registro.
    /// </summary>
    public void SetHidden(bool hidden, DateTimeOffset now)
    {
        if (hidden == _hidden)
        {
            return;
        }

        _hidden = hidden;
        if (hidden)
        {
            LeaveInteractive(now);
            if (_shown is { Activity.Kind: ActivityKind.Transient } shown)
            {
                _queue.Add(shown);
            }

            _shown = null;
            _state = IslandState.Compact;
            DropLowPriorityQueued(now);
        }
        else
        {
            ShowNextOrCompact(now);
        }

        Publish();
    }

    public void PointerEntered() => _pointerOver = true;

    public void PointerExited(DateTimeOffset now)
    {
        _pointerOver = false;

        // Ao sair, o usuário ganha um tempo antes de recolher, em vez de expirar na hora.
        if (_state == IslandState.Expanded && _shownUntil < now + HoverGrace)
        {
            _shownUntil = now + HoverGrace;
        }
        else if (_state == IslandState.Interactive)
        {
            _collapseAt = now + ExpandedDuration;
        }
    }

    public void Advance(DateTimeOffset now)
    {
        if (_pointerOver || _hidden)
        {
            return;
        }

        if (_state == IslandState.Expanded && now >= _shownUntil)
        {
            if (_shown is { } shown)
            {
                Record(shown, ActivityRecordState.Completed, now);
            }

            ShowNextOrCompact(now);
        }
        else if (_state == IslandState.Interactive && _collapseAt is { } collapseAt && now >= collapseAt)
        {
            LeaveInteractive(now);
            ShowNextOrCompact(now);
        }

        Publish();
    }

    /// <summary>A configuração mudou (ex.: módulo fixado): recalcula o que é exibido.</summary>
    public void Refresh() => Publish();

    private static readonly TimeSpan HoverGrace = TimeSpan.FromSeconds(2);

    private void Announce(Entry entry, DateTimeOffset now)
    {
        if (_hidden)
        {
            Enqueue(entry, now);
            if (entry.Priority < ActivityPriority.High)
            {
                DropLowPriorityQueued(now);
            }

            return;
        }

        if (_state == IslandState.Interactive)
        {
            if (entry.Priority < ActivityPriority.High)
            {
                Enqueue(entry, now);
                return;
            }

            LeaveInteractive(now);
        }

        if (_shown is null)
        {
            Show(entry, now);
        }
        else if (entry.Priority > _shown.Priority)
        {
            if (_shown.Activity.Kind == ActivityKind.Transient)
            {
                Enqueue(_shown, now);
            }

            Show(entry, now);
        }
        else
        {
            Enqueue(entry, now);
        }
    }

    private void Enqueue(Entry entry, DateTimeOffset now)
    {
        _queue.Add(entry);
        Record(entry, ActivityRecordState.Queued, now);
    }

    private void DropLowPriorityQueued(DateTimeOffset now)
    {
        foreach (var dropped in _queue.Where(e => e.Priority < ActivityPriority.High && e.Activity.Kind == ActivityKind.Transient).ToList())
        {
            _queue.Remove(dropped);
            Record(dropped, ActivityRecordState.Dropped, now);
        }
    }

    private void Show(Entry entry, DateTimeOffset now)
    {
        _shown = entry;
        _shownUntil = now + DurationOf(entry.Activity);
        _state = IslandState.Expanded;
        Record(entry, ActivityRecordState.Shown, now);
    }

    private void ShowNextOrCompact(DateTimeOffset now)
    {
        var next = _hidden
            ? null
            : _queue
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

    private void LeaveInteractive(DateTimeOffset now)
    {
        if (_state != IslandState.Interactive)
        {
            return;
        }

        // Um painel/aviso aberto no interativo termina ao fechar.
        if (_shown is { Activity.Kind: ActivityKind.Transient } shown)
        {
            Record(shown, ActivityRecordState.Completed, now);
        }

        _focusId = null;
        _collapseAt = null;
        _shown = null;
        _state = IslandState.Compact;
    }

    /// <summary>Abas do estado interativo: o painel/aviso aberto (se houver) e as atividades contínuas.</summary>
    private List<string> InteractivePages()
    {
        var pages = OrderedLive().Select(e => e.Id).ToList();
        if (_shown is { Activity.Kind: ActivityKind.Transient } shown)
        {
            pages.Insert(0, shown.Id);
        }

        return pages;
    }

    private void RemoveCore(string activityId, DateTimeOffset now, ActivityRecordState reason, bool keepLive = false)
    {
        var removed = _live.GetValueOrDefault(activityId)
            ?? _queue.FirstOrDefault(e => e.Id == activityId)
            ?? (_shown?.Id == activityId ? _shown : null);

        if (!keepLive)
        {
            _live.Remove(activityId);
            _dismissedLive.Remove(activityId);
        }

        _queue.RemoveAll(e => e.Id == activityId);
        if (removed is not null)
        {
            Record(removed, reason, now);
        }

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
                LeaveInteractive(now);
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

        _queue[index] = entry with { Sequence = _queue[index].Sequence, GroupCount = _queue[index].GroupCount };
        return true;
    }

    private void Record(Entry entry, ActivityRecordState state, DateTimeOffset now)
    {
        var activity = entry.Activity;
        var existing = _history.First;
        while (existing is not null && existing.Value.Id != activity.Id)
        {
            existing = existing.Next;
        }

        var occurredAt = existing?.Value.OccurredAt ?? entry.PostedAt;
        if (existing is not null && state is ActivityRecordState.Queued or ActivityRecordState.Shown && existing.Value.State is not (ActivityRecordState.Queued or ActivityRecordState.Shown))
        {
            // Mesma atividade publicada de novo depois de terminar: é uma nova ocorrência.
            occurredAt = entry.PostedAt;
            existing = null;
        }

        if (existing is not null)
        {
            _history.Remove(existing);
        }

        _history.AddFirst(new ActivityRecord(activity.Id, activity.ModuleId, activity.Kind, activity.Priority, occurredAt, state, now));
        while (_history.Count > HistoryCapacity)
        {
            _history.RemoveLast();
        }
    }

    private TimeSpan DurationOf(IslandActivity activity) => activity.Duration ?? ExpandedDuration;

    private IEnumerable<Entry> OrderedLive()
    {
        var pinned = settings().PinnedModuleId;
        return _live.Values
            .Where(e => !_dismissedLive.Contains(e.Id))
            .OrderByDescending(e => pinned is not null && e.Activity.ModuleId == pinned)
            .ThenByDescending(e => e.Priority)
            .ThenByDescending(e => e.Sequence);
    }

    private Entry? TopLive() => OrderedLive().FirstOrDefault();

    private Entry? FocusedEntry => _state switch
    {
        IslandState.Expanded => _shown,
        IslandState.Interactive when _focusId is not null =>
            _shown?.Id == _focusId ? _shown : _live.GetValueOrDefault(_focusId),
        _ => null,
    };

    private IslandActivity? Focused => FocusedEntry?.Activity;

    private void Publish()
    {
        var focusedEntry = FocusedEntry;
        var focused = focusedEntry?.Activity;
        var indicator = TopLive()?.Activity;
        var content = _state == IslandState.Compact ? indicator?.Content : focused?.Content;
        var dimensions = IslandLayout.Clamp(content?.GetPreferredDimensions(_state) ?? IslandLayout.For(_state));

        var pages = _state == IslandState.Interactive ? InteractivePages() : [];
        var pageIndex = _focusId is null ? -1 : pages.IndexOf(_focusId);
        var pinned = indicator is not null && indicator.ModuleId == settings().PinnedModuleId;
        var snapshot = new IslandSnapshot(
            _state,
            focused,
            indicator,
            dimensions,
            _live.Count - _dismissedLive.Count(_live.ContainsKey),
            pages.Count,
            pageIndex,
            focusedEntry?.GroupCount ?? 1,
            pinned);
        if (snapshot == Snapshot)
        {
            return;
        }

        Snapshot = snapshot;
        Changed?.Invoke(this, snapshot);
    }

    private sealed record Entry(IslandActivity Activity, long Sequence, DateTimeOffset PostedAt)
    {
        public int GroupCount { get; init; } = 1;

        public string Id => Activity.Id;

        public ActivityPriority Priority => Activity.Priority;
    }
}
