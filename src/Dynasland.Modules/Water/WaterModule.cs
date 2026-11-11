using Dynasland.Core.Activities;
using Dynasland.Core.Events;
using Dynasland.Core.Interfaces;
using Dynasland.Core.Models;
using Dynasland.Core.Modules;
using Dynasland.Core.Session;
using Dynasland.Core.Shortcuts;
using Dynasland.Modules.Common;
using Microsoft.Extensions.Logging;

namespace Dynasland.Modules.Water;

/// <summary>
/// Lembrete de água: avisa na ilha em horários fixos do dia ativo (Bebi soma um copo, Adiar repete em
/// 10 min), conta os copos do dia contra a meta e mostra o progresso na tela inicial e na Central. O atalho
/// da tela inicial abre um painel com um corpo que se enche de água conforme os copos do dia.
/// </summary>
/// <remarks>
/// Um único timer, reagendado quando a contagem ou as opções mudam e ao retomar da suspensão. Passada a
/// meta, o próximo evento é a virada do dia. Avisos que venceram com o Dynasland fechado são descartados.
/// O painel conta como aberto de <see cref="OpenPanel"/> até a ilha deixar de exibi-lo
/// (<see cref="IActivityContent.OnPresentationChanged"/> com <c>null</c>) ou o usuário fechá-lo
/// (<see cref="ActivityDismissedEvent"/>); enquanto aberto, registrar um copo não posta a confirmação por cima.
/// </remarks>
public sealed class WaterModule(
    IWaterStore store,
    IHubService hub,
    IEventBus bus,
    IUiDispatcher ui,
    TimeProvider time,
    IShortcutRegistry shortcuts,
    IGlanceRegistry glances,
    IAlertSound sound,
    IModuleOptions options,
    IGlobalShortcutRegistry globalShortcuts,
    ILogger<WaterModule> logger) : IIslandModule
{
    public const string ModuleId = "water";
    public const string GlanceId = "water.today";
    public const string PanelActivityId = "water.panel";

    private const string ActivityId = "water.reminder";
    private const int DefaultStartHour = 8;
    private const int DefaultEndHour = 22;
    private static readonly TimeSpan ReminderDisplayDuration = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan ConfirmationDuration = TimeSpan.FromSeconds(4);
    private static readonly TimeSpan DrankDuration = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan SnoozeDelay = TimeSpan.FromMinutes(10);

    public static readonly ModuleOption Sound = ModuleOption.Toggle(
        "sound", "Som nos lembretes", "Toca o som de alerta do Windows quando o lembrete aparece.");

    public static readonly ModuleOption Interval = ModuleOption.Slider(
        "intervalMinutes", "Intervalo entre lembretes", "De quanto em quanto tempo lembrar, a partir do início do dia.", 60, 15, 180, 15, "min");

    public static readonly ModuleOption StartHour = ModuleOption.Slider(
        "startHour", "Início do dia", "Hora do primeiro lembrete do dia.", DefaultStartHour, 0, 23, 1, "h");

    public static readonly ModuleOption EndHour = ModuleOption.Slider(
        "endHour", "Fim do dia", "A partir desta hora não há mais lembretes. Precisa ser depois do início.", DefaultEndHour, 1, 24, 1, "h");

    public static readonly ModuleOption Goal = ModuleOption.Slider(
        "goal", "Meta diária", "Quantos copos beber por dia.", 8, 1, 20, 1, "copos");

    private readonly Lock _lock = new();
    private readonly SemaphoreSlim _reschedule = new(1, 1); // serializa RescheduleAsync; nunca descartado (chamadas atrasadas)
    private readonly List<IDisposable> _registrations = [];
    private ITimer? _timer;
    private bool _running;
    private DateTimeOffset? _snoozeUntil;
    private DateTimeOffset _lastReminder = DateTimeOffset.MinValue;
    private WaterBodyPanelContent? _panel;
    private bool _panelOpen;

    public string Id => ModuleId;

    public string DisplayName => "Água";

    public string Description => "Lembra de beber água e conta os copos do dia. O botão Água da ilha mostra um corpo que se enche conforme você bebe (Ctrl+Alt+G registra um copo).";

    public IReadOnlyList<ModuleOption> Options => [Sound, Interval, StartHour, EndHour, Goal];

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _running = true;
        lock (_lock)
        {
            _panel = new WaterBodyPanelContent(() => _ = DrinkAsync(), () => _ = UndoAsync(), OnPanelPresentationChanged);
            _panelOpen = false;
        }

        store.Changed += OnChanged;
        options.Changed += OnOptionChanged;
        _registrations.Add(shortcuts.Register(new IslandShortcut(PanelActivityId, ModuleId, VectorIcons.Water, "Água", OpenPanel)));
        _registrations.Add(globalShortcuts.Register(new GlobalShortcut(
            "water.drink", "Registrar um copo de água", "Água",
            new HotkeyGesture(HotkeyModifiers.Control | HotkeyModifiers.Alt, 'G'), () => _ = DrinkAsync())));
        _registrations.Add(bus.Subscribe<ActivityDismissedEvent>(e =>
        {
            if (e.Activity.Id == PanelActivityId)
            {
                SetPanelOpen(false);
            }
        }));
        _registrations.Add(bus.Subscribe<SessionChangedEvent>(e =>
        {
            if (e.Change == SessionChange.Resumed)
            {
                _ = RescheduleAsync();
            }
        }));
        _ = RescheduleAsync();
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _running = false;
        store.Changed -= OnChanged;
        options.Changed -= OnOptionChanged;
        foreach (var registration in _registrations)
        {
            registration.Dispose();
        }

        _registrations.Clear();
        lock (_lock)
        {
            _timer?.Dispose();
            _timer = null;
            _panel = null;
            _panelOpen = false;
        }

        glances.Remove(GlanceId);
        return Task.CompletedTask;
    }

    private void OnChanged(object? sender, EventArgs e) => _ = RescheduleAsync();

    private void OnOptionChanged(object? sender, (string ModuleId, string Key) change)
    {
        if (change.ModuleId == ModuleId)
        {
            _ = RescheduleAsync();
        }
    }

    /// <summary>Abre na ilha o painel do corpo que se enche de água (atalho da tela inicial).</summary>
    public void OpenPanel()
    {
        WaterBodyPanelContent? panel;
        lock (_lock)
        {
            panel = _panel;
            _panelOpen = panel is not null;
        }

        if (panel is not null)
        {
            bus.PostActivity(new IslandActivity(PanelActivityId, ModuleId, ActivityKind.Transient, panel) { OpenInteractive = true });
        }
    }

    private void OnPanelPresentationChanged(IslandState? state) => SetPanelOpen(state is not null);

    private void SetPanelOpen(bool open)
    {
        lock (_lock)
        {
            _panelOpen = open && _panel is not null;
        }
    }

    /// <summary>Registra um copo (Bebi do aviso e do painel, atalho global).</summary>
    public async Task DrinkAsync()
    {
        try
        {
            var glasses = await store.AddAsync(DateOnly.FromDateTime(time.GetLocalNow().DateTime), 1);
            var goal = (int)options.Get(ModuleId, Goal);
            bool panelOpen;
            lock (_lock)
            {
                _snoozeUntil = null;
                panelOpen = _panelOpen;
            }

            // Com o painel aberto, o próprio corpo mostra o copo novo: a confirmação ficaria por cima dele.
            if (panelOpen)
            {
                return;
            }

            bus.PostActivity(new IslandActivity(ActivityId, ModuleId, ActivityKind.Transient, new WaterDrankContent(glasses, goal))
            {
                Duration = DrankDuration,
                GroupKey = ActivityId,
            });
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Falha ao registrar o copo de água.");
        }
    }

    /// <summary>Desfaz o último copo do dia (botão Desfazer do painel).</summary>
    private async Task UndoAsync()
    {
        try
        {
            await store.AddAsync(DateOnly.FromDateTime(time.GetLocalNow().DateTime), -1);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Falha ao desfazer o copo de água.");
        }
    }

    private void Snooze()
    {
        lock (_lock)
        {
            _snoozeUntil = time.GetLocalNow() + SnoozeDelay;
        }

        PostStatus("Adiado", "Aviso de novo em 10 min");
        _ = RescheduleAsync();
    }

    /// <summary>Atualiza o glance e agenda o próximo evento (aviso, fim do adiamento ou virada do dia).</summary>
    public async Task RescheduleAsync()
    {
        if (!_running)
        {
            return;
        }

        await _reschedule.WaitAsync();
        try
        {
            if (!_running)
            {
                return;
            }

            var now = time.GetLocalNow();
            var (start, end, interval, goal) = ReadSettings();
            var glasses = await store.GetAsync(DateOnly.FromDateTime(now.DateTime));
            if (!_running)
            {
                return;
            }

            glances.Set(new IslandGlance(GlanceId, "", $"{glasses}/{goal} copos", Order: 2, Invoke: () => hub.Open(HubPage.Water)));
            UpdatePanel(glasses, goal);

            lock (_lock)
            {
                DateTimeOffset next;
                if (glasses >= goal)
                {
                    next = new DateTimeOffset(now.Date.AddDays(1), now.Offset);
                }
                else if (_snoozeUntil is { } snoozed && snoozed > now && WaterSchedule.IsActive(snoozed, start, end))
                {
                    next = snoozed;
                }
                else
                {
                    next = WaterSchedule.Next(now, start, end, interval);
                }

                // A virada do dia sempre agenda um evento, para o glance zerar mesmo sem aviso à meia-noite.
                var midnight = new DateTimeOffset(now.Date.AddDays(1), now.Offset);
                if (next > midnight)
                {
                    next = midnight;
                }

                var delay = next - time.GetLocalNow();
                if (delay < TimeSpan.Zero)
                {
                    delay = TimeSpan.Zero;
                }

                _timer?.Dispose();
                _timer = _running
                    ? time.CreateTimer(_ => _ = OnTimerAsync(), null, delay, Timeout.InfiniteTimeSpan)
                    : null;
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Falha ao agendar os lembretes de água.");
        }
        finally
        {
            _reschedule.Release();
        }
    }

    /// <summary>O painel acompanha a contagem e a meta ao vivo (mesmo fechado, para abrir já no nível certo).</summary>
    private void UpdatePanel(int glasses, int goal)
    {
        WaterBodyPanelContent? panel;
        lock (_lock)
        {
            panel = _panel;
        }

        if (panel is not null)
        {
            ui.Post(() => panel.Apply(glasses, goal));
        }
    }

    private async Task OnTimerAsync()
    {
        try
        {
            var now = time.GetLocalNow();
            var (start, end, _, goal) = ReadSettings();
            var glasses = await store.GetAsync(DateOnly.FromDateTime(now.DateTime));
            var due = false;
            if (_running && glasses < goal && WaterSchedule.IsActive(now, start, end))
            {
                lock (_lock)
                {
                    if (now - _lastReminder > TimeSpan.FromMinutes(1))
                    {
                        _lastReminder = now;
                        _snoozeUntil = null;
                        due = true;
                    }
                }
            }

            if (due && _running)
            {
                Announce(glasses, goal);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Falha ao disparar o lembrete de água.");
        }

        await RescheduleAsync();
    }

    private void Announce(int glasses, int goal)
    {
        var content = new WaterReminderContent(() => _ = DrinkAsync(), Snooze, $"{glasses} de {goal} copos hoje");
        bus.PostActivity(new IslandActivity(ActivityId, ModuleId, ActivityKind.Transient, content)
        {
            Priority = ActivityPriority.High,
            Duration = ReminderDisplayDuration,
            OpenInteractive = true,
            GroupKey = ActivityId,
        });

        if (options.IsOn(ModuleId, Sound))
        {
            sound.Play();
        }
    }

    /// <summary>Resposta curta que substitui o aviso (mesmo Id) depois de Adiar.</summary>
    private void PostStatus(string title, string message) =>
        bus.PostActivity(new IslandActivity(ActivityId, ModuleId, ActivityKind.Transient, new StatusContent(StatusKind.Info, title, message))
        {
            Duration = ConfirmationDuration,
            GroupKey = ActivityId,
        });

    private (int Start, int End, int Interval, int Goal) ReadSettings()
    {
        var start = (int)options.Get(ModuleId, StartHour);
        var end = (int)options.Get(ModuleId, EndHour);
        if (end <= start)
        {
            (start, end) = (DefaultStartHour, DefaultEndHour);
        }

        return (start, end, (int)options.Get(ModuleId, Interval), (int)options.Get(ModuleId, Goal));
    }
}
