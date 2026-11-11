using Dynasland.Core.Activities;
using Dynasland.Core.Events;
using Dynasland.Core.Interfaces;
using Dynasland.Core.Modules;
using Dynasland.Core.Session;
using Dynasland.Core.Shortcuts;
using Microsoft.Extensions.Logging;

namespace Dynasland.Modules.Water;

/// <summary>
/// Lembrete de água: avisa na ilha em horários fixos do dia ativo (Bebi soma um copo, Adiar repete em
/// 10 min), conta os copos do dia contra a meta e mostra o progresso na tela inicial e na Central.
/// </summary>
/// <remarks>
/// Um único timer, reagendado quando a contagem ou as opções mudam e ao retomar da suspensão. Passada a
/// meta, o próximo evento é a virada do dia. Avisos que venceram com o Dynasland fechado são descartados.
/// </remarks>
public sealed class WaterModule(
    IWaterStore store,
    IHubService hub,
    IEventBus bus,
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

    private const string ActivityId = "water.reminder";
    private const int DefaultStartHour = 8;
    private const int DefaultEndHour = 22;
    private static readonly TimeSpan ReminderDisplayDuration = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan ConfirmationDuration = TimeSpan.FromSeconds(4);
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
    private readonly List<IDisposable> _registrations = [];
    private ITimer? _timer;
    private bool _running;
    private DateTimeOffset? _snoozeUntil;
    private DateTimeOffset _lastReminder = DateTimeOffset.MinValue;

    public string Id => ModuleId;

    public string DisplayName => "Água";

    public string Description => "Lembra de beber água e conta os copos do dia. Acompanhe na Central (Ctrl+Alt+G registra um copo).";

    public IReadOnlyList<ModuleOption> Options => [Sound, Interval, StartHour, EndHour, Goal];

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _running = true;
        store.Changed += OnChanged;
        options.Changed += OnOptionChanged;
        _registrations.Add(shortcuts.Register(new IslandShortcut("water.drink", ModuleId, "", "Bebi um copo", () => _ = DrinkAsync())));
        _registrations.Add(globalShortcuts.Register(new GlobalShortcut(
            "water.drink", "Registrar um copo de água", "Água",
            new HotkeyGesture(HotkeyModifiers.Control | HotkeyModifiers.Alt, 'G'), () => _ = DrinkAsync())));
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

    /// <summary>Registra um copo (botão Bebi, atalho da ilha e atalho global).</summary>
    public async Task DrinkAsync()
    {
        try
        {
            var glasses = await store.AddAsync(DateOnly.FromDateTime(time.GetLocalNow().DateTime), 1);
            var goal = (int)options.Get(ModuleId, Goal);
            lock (_lock)
            {
                _snoozeUntil = null;
            }

            PostStatus(glasses >= goal ? "Meta de água batida!" : "Boa!", $"{glasses} de {goal} copos hoje");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Falha ao registrar o copo de água.");
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

        try
        {
            var now = time.GetLocalNow();
            var (start, end, interval, goal) = ReadSettings();
            var glasses = await store.GetAsync(DateOnly.FromDateTime(now.DateTime));
            glances.Set(new IslandGlance(GlanceId, "", $"{glasses}/{goal} copos", Order: 2, Invoke: () => hub.Open(HubPage.Water)));

            DateTimeOffset next;
            lock (_lock)
            {
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

                _timer?.Dispose();
                _timer = _running
                    ? time.CreateTimer(_ => _ = OnTimerAsync(), null, next - now, Timeout.InfiniteTimeSpan)
                    : null;
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Falha ao agendar os lembretes de água.");
        }
    }

    private async Task OnTimerAsync()
    {
        try
        {
            var now = time.GetLocalNow();
            var (start, end, _, goal) = ReadSettings();
            var glasses = await store.GetAsync(DateOnly.FromDateTime(now.DateTime));
            var due = glasses < goal
                && WaterSchedule.IsActive(now, start, end)
                && now - _lastReminder > TimeSpan.FromMinutes(1);
            if (due)
            {
                _lastReminder = now;
                lock (_lock)
                {
                    _snoozeUntil = null;
                }

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

    /// <summary>Resposta curta que substitui o aviso (mesmo Id) depois de Bebi ou Adiar.</summary>
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
