using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dynasland.Core.Interfaces;
using Dynasland.Core.Models;
using Microsoft.Extensions.Logging;

namespace Dynasland.App.ViewModels.Hub;

/// <summary>Célula do calendário mensal.</summary>
public sealed partial class CalendarDayViewModel(DateOnly date, bool isCurrentMonth, bool isToday, int eventCount, IRelayCommand select) : ObservableObject
{
    public DateOnly Date => date;

    public string DayNumber => date.Day.ToString(CultureInfo.InvariantCulture);

    public bool IsCurrentMonth => isCurrentMonth;

    public bool IsToday => isToday;

    public bool HasEvents => eventCount > 0;

    public string Tooltip => eventCount switch
    {
        0 => date.ToString("dd/MM", CultureInfo.InvariantCulture),
        1 => "1 compromisso",
        _ => $"{eventCount} compromissos",
    };

    public IRelayCommand Select => select;

    [ObservableProperty]
    private bool _isSelected;
}

public sealed class CalendarEventItemViewModel(CalendarEvent calendarEvent, IRelayCommand edit, IRelayCommand delete)
{
    public CalendarEvent Event => calendarEvent;

    public string Title => calendarEvent.Title;

    public string TimeText => calendarEvent.AllDay
        ? "Dia inteiro"
        : $"{calendarEvent.StartsAt.ToLocalTime():HH:mm} – {calendarEvent.EndsAt.ToLocalTime():HH:mm}";

    public string ReminderText => CalendarPageViewModel.ReminderName(calendarEvent.ReminderMinutes);

    public string Notes => calendarEvent.Notes;

    public bool HasNotes => !string.IsNullOrWhiteSpace(calendarEvent.Notes);

    public IRelayCommand Edit => edit;

    public IRelayCommand Delete => delete;
}

public sealed record ReminderChoice(int? Minutes, string Name);

/// <summary>Página Agenda da Central: calendário mensal, compromissos do dia e formulário de edição.</summary>
public sealed partial class CalendarPageViewModel : ObservableObject, IHubPage
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    private readonly ICalendarStore _store;
    private readonly IUiDispatcher _ui;
    private readonly TimeProvider _time;
    private readonly ILogger<CalendarPageViewModel> _logger;
    private long _editingId;

    public CalendarPageViewModel(ICalendarStore store, IUiDispatcher ui, TimeProvider time, ILogger<CalendarPageViewModel> logger)
    {
        _store = store;
        _ui = ui;
        _time = time;
        _logger = logger;
        var today = Today;
        _month = new DateOnly(today.Year, today.Month, 1);
        _selectedDate = today;
        _editReminder = Reminders[3];
        store.Changed += OnStoreChanged;
    }

    public HubPage Page => HubPage.Calendar;

    public Func<string, bool>? Confirm { get; set; }

    public static IReadOnlyList<ReminderChoice> Reminders { get; } =
    [
        new(null, "Sem lembrete"),
        new(0, "Na hora"),
        new(5, "5 minutos antes"),
        new(10, "10 minutos antes"),
        new(15, "15 minutos antes"),
        new(30, "30 minutos antes"),
        new(60, "1 hora antes"),
        new(1440, "1 dia antes"),
    ];

    public static IReadOnlyList<string> WeekDays { get; } = ["dom", "seg", "ter", "qua", "qui", "sex", "sáb"];

    public ObservableCollection<CalendarDayViewModel> Days { get; } = [];

    public ObservableCollection<CalendarEventItemViewModel> DayEvents { get; } = [];

    public ObservableCollection<CalendarEventItemViewModel> Upcoming { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MonthTitle))]
    private DateOnly _month;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedDateTitle))]
    private DateOnly _selectedDate;

    [ObservableProperty]
    private bool _isEditing;

    [ObservableProperty]
    private string _editTitle = string.Empty;

    [ObservableProperty]
    private string _editStart = "09:00";

    [ObservableProperty]
    private string _editEnd = "10:00";

    [ObservableProperty]
    private bool _editAllDay;

    [ObservableProperty]
    private ReminderChoice _editReminder;

    [ObservableProperty]
    private string _editNotes = string.Empty;

    [ObservableProperty]
    private string? _editError;

    [ObservableProperty]
    private bool _hasDayEvents;

    public string MonthTitle
    {
        get
        {
            var text = Month.ToDateTime(TimeOnly.MinValue).ToString("MMMM 'de' yyyy", PtBr);
            return char.ToUpper(text[0], PtBr) + text[1..];
        }
    }

    public string SelectedDateTitle
    {
        get
        {
            var text = SelectedDate.ToDateTime(TimeOnly.MinValue).ToString("dddd, d 'de' MMMM", PtBr);
            return char.ToUpper(text[0], PtBr) + text[1..];
        }
    }

    public string EditorTitle => _editingId == 0 ? "Novo compromisso" : "Editar compromisso";

    private DateOnly Today => DateOnly.FromDateTime(_time.GetLocalNow().DateTime);

    public static string ReminderName(int? minutes) => Reminders.FirstOrDefault(r => r.Minutes == minutes)?.Name ?? $"{minutes} minutos antes";

    public async Task LoadAsync()
    {
        try
        {
            var firstCell = Month.AddDays(-(int)Month.DayOfWeek);
            var from = ToLocalDateTime(firstCell, TimeSpan.Zero);
            var to = from.AddDays(42);
            var events = await _store.ListAsync(from, to);
            var upcoming = await _store.ListAsync(_time.GetUtcNow(), _time.GetUtcNow().AddDays(14));

            _ui.Post(() =>
            {
                Days.Clear();
                for (var i = 0; i < 42; i++)
                {
                    var date = firstCell.AddDays(i);
                    var count = events.Count(e => Covers(e, date));
                    Days.Add(new CalendarDayViewModel(date, date.Month == Month.Month, date == Today, count, new RelayCommand(() => SelectDay(date))) { IsSelected = date == SelectedDate });
                }

                DayEvents.Clear();
                foreach (var calendarEvent in events.Where(e => Covers(e, SelectedDate)).OrderBy(e => !e.AllDay).ThenBy(e => e.StartsAt))
                {
                    DayEvents.Add(ItemFor(calendarEvent));
                }

                HasDayEvents = DayEvents.Count > 0;
                Upcoming.Clear();
                foreach (var calendarEvent in upcoming.Take(6))
                {
                    Upcoming.Add(ItemFor(calendarEvent));
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao carregar a agenda.");
        }
    }

    [RelayCommand]
    private Task PreviousMonthAsync()
    {
        Month = Month.AddMonths(-1);
        return LoadAsync();
    }

    [RelayCommand]
    private Task NextMonthAsync()
    {
        Month = Month.AddMonths(1);
        return LoadAsync();
    }

    [RelayCommand]
    private Task GoToTodayAsync()
    {
        var today = Today;
        Month = new DateOnly(today.Year, today.Month, 1);
        SelectedDate = today;
        return LoadAsync();
    }

    [RelayCommand]
    private void New()
    {
        _editingId = 0;
        EditTitle = string.Empty;
        EditStart = "09:00";
        EditEnd = "10:00";
        EditAllDay = false;
        EditReminder = Reminders[3];
        EditNotes = string.Empty;
        EditError = null;
        IsEditing = true;
        OnPropertyChanged(nameof(EditorTitle));
    }

    [RelayCommand]
    private void CancelEdit() => IsEditing = false;

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(EditTitle))
        {
            EditError = "Dê um título ao compromisso.";
            return;
        }

        DateTimeOffset start, end;
        if (EditAllDay)
        {
            start = ToLocalDateTime(SelectedDate, TimeSpan.Zero);
            end = start.AddDays(1);
        }
        else
        {
            if (!TryParseTime(EditStart, out var startTime) || !TryParseTime(EditEnd, out var endTime))
            {
                EditError = "Use horários no formato 14:30.";
                return;
            }

            start = ToLocalDateTime(SelectedDate, startTime);
            end = ToLocalDateTime(SelectedDate, endTime);
            if (end <= start)
            {
                end = end.AddDays(1); // termina depois da meia-noite
            }
        }

        try
        {
            await _store.SaveAsync(new CalendarEvent(_editingId, EditTitle.Trim(), EditNotes.Trim(), start, end, EditAllDay, EditReminder.Minutes, null));
            IsEditing = false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao salvar compromisso.");
            EditError = "Não foi possível salvar.";
        }
    }

    public void Dispose() => _store.Changed -= OnStoreChanged;

    internal static bool TryParseTime(string text, out TimeSpan time) =>
        TimeSpan.TryParseExact(text.Trim(), [@"h\:mm", @"hh\:mm", "hh", "%h"], CultureInfo.InvariantCulture, out time) && time < TimeSpan.FromDays(1);

    private static bool Covers(CalendarEvent calendarEvent, DateOnly date)
    {
        var dayStart = new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue), calendarEvent.StartsAt.ToLocalTime().Offset);
        return calendarEvent.StartsAt.ToLocalTime() < dayStart.AddDays(1) && calendarEvent.EndsAt.ToLocalTime() > dayStart;
    }

    private DateTimeOffset ToLocalDateTime(DateOnly date, TimeSpan timeOfDay)
    {
        var local = date.ToDateTime(TimeOnly.MinValue) + timeOfDay;
        return new DateTimeOffset(local, _time.LocalTimeZone.GetUtcOffset(local));
    }

    private void SelectDay(DateOnly date)
    {
        SelectedDate = date;
        foreach (var day in Days)
        {
            day.IsSelected = day.Date == date;
        }

        if (date.Month != Month.Month || date.Year != Month.Year)
        {
            Month = new DateOnly(date.Year, date.Month, 1);
        }

        IsEditing = false;
        _ = LoadAsync();
    }

    private CalendarEventItemViewModel ItemFor(CalendarEvent calendarEvent) => new(
        calendarEvent,
        new RelayCommand(() => StartEdit(calendarEvent)),
        new RelayCommand(() => _ = DeleteAsync(calendarEvent)));

    private void StartEdit(CalendarEvent calendarEvent)
    {
        _editingId = calendarEvent.Id;
        SelectedDate = DateOnly.FromDateTime(calendarEvent.StartsAt.ToLocalTime().DateTime);
        EditTitle = calendarEvent.Title;
        EditStart = calendarEvent.StartsAt.ToLocalTime().ToString("HH:mm", PtBr);
        EditEnd = calendarEvent.EndsAt.ToLocalTime().ToString("HH:mm", PtBr);
        EditAllDay = calendarEvent.AllDay;
        EditReminder = Reminders.FirstOrDefault(r => r.Minutes == calendarEvent.ReminderMinutes) ?? Reminders[0];
        EditNotes = calendarEvent.Notes;
        EditError = null;
        IsEditing = true;
        OnPropertyChanged(nameof(EditorTitle));
    }

    private async Task DeleteAsync(CalendarEvent calendarEvent)
    {
        if (Confirm?.Invoke($"Excluir “{calendarEvent.Title}”?") == false)
        {
            return;
        }

        await _store.DeleteAsync(calendarEvent.Id);
    }

    private void OnStoreChanged(object? sender, EventArgs e) => _ = LoadAsync();
}
