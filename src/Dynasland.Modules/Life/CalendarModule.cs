using System.Globalization;
using Dynasland.Core.Activities;
using Dynasland.Core.Events;
using Dynasland.Core.Interfaces;
using Dynasland.Core.Models;
using Dynasland.Core.Modules;
using Dynasland.Core.Session;
using Dynasland.Core.Shortcuts;
using Microsoft.Extensions.Logging;

namespace Dynasland.Modules.Life;

/// <summary>
/// Agenda do Life Center: lembra os compromissos na ilha no horário configurado e mostra o próximo na
/// tela inicial. Os compromissos são criados e editados na Central (página Agenda), sem conexão externa.
/// </summary>
/// <remarks>
/// O próximo lembrete é agendado por instante real (um único timer) e reagendado quando a agenda muda e
/// ao retomar da suspensão. Lembretes que venceram com o Dynasland fechado disparam ao iniciar, se o
/// compromisso ainda não começou; os de compromissos já passados são apenas marcados como vistos.
/// </remarks>
public sealed class CalendarModule(
    ICalendarStore store,
    IHubService hub,
    IEventBus bus,
    TimeProvider time,
    IShortcutRegistry shortcuts,
    IGlanceRegistry glances,
    IAlertSound sound,
    IModuleOptions options,
    IGlobalShortcutRegistry globalShortcuts,
    ILogger<CalendarModule> logger) : IIslandModule
{
    public const string ModuleId = "calendar";
    public const string GlanceId = "calendar.next";

    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");
    private static readonly TimeSpan ReminderDisplayDuration = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan GlanceHorizon = TimeSpan.FromDays(2);

    public static readonly ModuleOption ReminderSound = ModuleOption.Toggle(
        "reminderSound", "Som nos lembretes", "Toca o som de alerta do Windows quando um lembrete aparece.");

    private readonly Lock _lock = new();
    private readonly List<IDisposable> _registrations = [];
    private ITimer? _timer;
    private bool _running;

    public string Id => ModuleId;

    public string DisplayName => "Agenda";

    public string Description => "Compromissos locais com lembretes na ilha. Crie e edite na Central (Ctrl+Alt+A).";

    public IReadOnlyList<ModuleOption> Options => [ReminderSound];

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _running = true;
        store.Changed += OnStoreChanged;
        _registrations.Add(shortcuts.Register(new IslandShortcut("calendar.open", ModuleId, "", "Agenda", () => hub.Open(HubPage.Calendar))));
        _registrations.Add(globalShortcuts.Register(new GlobalShortcut(
            "calendar.open", "Abrir a agenda", "Agenda",
            new HotkeyGesture(HotkeyModifiers.Control | HotkeyModifiers.Alt, 'A'), () => hub.Open(HubPage.Calendar))));
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
        store.Changed -= OnStoreChanged;
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

    private void OnStoreChanged(object? sender, EventArgs e) => _ = RescheduleAsync();

    /// <summary>Dispara os lembretes vencidos, agenda o próximo e atualiza o próximo compromisso.</summary>
    public async Task RescheduleAsync()
    {
        if (!_running)
        {
            return;
        }

        try
        {
            var now = time.GetUtcNow();
            var pending = await store.PendingRemindersAsync();
            foreach (var due in pending.Where(e => e.ReminderAt <= now))
            {
                await store.MarkRemindedAsync(due.Id, now);
                if (due.StartsAt > now || due.AllDay && due.EndsAt > now)
                {
                    Announce(due, now);
                }
            }

            var next = pending.Where(e => e.ReminderAt > now).MinBy(e => e.ReminderAt);
            lock (_lock)
            {
                _timer?.Dispose();
                _timer = next?.ReminderAt is { } at && _running
                    ? time.CreateTimer(_ => _ = RescheduleAsync(), null, at - now, Timeout.InfiniteTimeSpan)
                    : null;
            }

            await UpdateGlanceAsync(now);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Falha ao agendar os lembretes da agenda.");
        }
    }

    private void Announce(CalendarEvent calendarEvent, DateTimeOffset now)
    {
        var status = new StatusContent(
            StatusKind.Info,
            calendarEvent.Title,
            DescribeStart(calendarEvent, now),
            new CommunityToolkit.Mvvm.Input.RelayCommand(() => hub.Open(HubPage.Calendar)),
            "Abrir agenda");
        bus.PostActivity(new IslandActivity($"calendar.reminder.{calendarEvent.Id}", ModuleId, ActivityKind.Transient, status)
        {
            Priority = ActivityPriority.High,
            Duration = ReminderDisplayDuration,
            GroupKey = "calendar.reminder",
        });

        if (options.IsOn(ModuleId, ReminderSound))
        {
            sound.Play();
        }
    }

    private async Task UpdateGlanceAsync(DateTimeOffset now)
    {
        var upcoming = (await store.ListAsync(now, now + GlanceHorizon))
            .Where(e => e.StartsAt >= now || e.EndsAt > now)
            .OrderBy(e => e.StartsAt)
            .FirstOrDefault();

        if (upcoming is null)
        {
            glances.Remove(GlanceId);
            return;
        }

        glances.Set(new IslandGlance(GlanceId, "", $"{upcoming.Title} · {DescribeStart(upcoming, now)}", Order: 1, Invoke: () => hub.Open(HubPage.Calendar)));
    }

    /// <summary>"em 15 min", "hoje às 14:00", "amanhã às 9:30", "hoje (dia inteiro)".</summary>
    public static string DescribeStart(CalendarEvent calendarEvent, DateTimeOffset now)
    {
        var start = calendarEvent.StartsAt.ToLocalTime();
        var today = now.ToLocalTime().Date;
        var day = start.Date == today ? "hoje" : start.Date == today.AddDays(1) ? "amanhã" : start.ToString("dd/MM", PtBr);
        if (calendarEvent.AllDay)
        {
            return $"{day} (dia inteiro)";
        }

        var until = calendarEvent.StartsAt - now;
        if (until > TimeSpan.Zero && until <= TimeSpan.FromMinutes(59))
        {
            return $"em {Math.Max(1, (int)Math.Ceiling(until.TotalMinutes))} min";
        }

        return until <= TimeSpan.Zero && calendarEvent.EndsAt > now ? "agora" : $"{day} às {start:HH:mm}";
    }
}
