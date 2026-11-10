using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dynasland.Core.Interfaces;
using Dynasland.Core.Models;
using Dynasland.Modules.Common;
using Microsoft.Extensions.Logging;

namespace Dynasland.App.ViewModels.Hub;

public sealed record FocusSessionItem(string Title, string TimeText, string DurationText, bool Completed);

/// <summary>Barra do gráfico de foco por dia (altura relativa de 0 a 1).</summary>
public sealed record FocusDayBar(string Label, double Height, string Tooltip, bool IsToday);

/// <summary>Página da Central com o histórico do Focus Center: hoje, últimos 7 dias e sessões recentes.</summary>
public sealed partial class FocusPageViewModel(IFocusSessionStore store, IUiDispatcher ui, TimeProvider time, ILogger<FocusPageViewModel> logger) : ObservableObject, IHubPage
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    public HubPage Page => HubPage.Focus;

    public Func<string, bool>? Confirm { get; set; }

    public ObservableCollection<FocusSessionItem> Sessions { get; } = [];

    public ObservableCollection<FocusDayBar> Days { get; } = [];

    [ObservableProperty]
    private string _todayFocus = "0 min";

    [ObservableProperty]
    private string _todayCount = "0";

    [ObservableProperty]
    private string _weekFocus = "0 min";

    [ObservableProperty]
    private string _weekCount = "0";

    [ObservableProperty]
    private bool _isEmpty = true;

    public async Task LoadAsync()
    {
        try
        {
            var today = time.GetLocalNow().Date;
            var offset = time.GetLocalNow().Offset;
            var since = new DateTimeOffset(today.AddDays(-6), offset);
            var sessions = await store.ListSinceAsync(since);

            var focus = sessions.Where(s => s.Kind == FocusSessionKind.Focus && s.Completed).ToList();
            var todayFocus = focus.Where(s => s.StartedAt.ToLocalTime().Date == today).ToList();
            var perDay = Enumerable.Range(0, 7)
                .Select(i => today.AddDays(i - 6))
                .Select(day => (Day: day, Minutes: focus.Where(s => s.StartedAt.ToLocalTime().Date == day).Sum(s => s.Actual.TotalMinutes)))
                .ToList();
            var max = Math.Max(perDay.Max(d => d.Minutes), 1);

            ui.Post(() =>
            {
                TodayCount = todayFocus.Count.ToString(PtBr);
                TodayFocus = TextFormat.Duration(TimeSpan.FromTicks(todayFocus.Sum(s => s.Actual.Ticks)));
                WeekCount = focus.Count.ToString(PtBr);
                WeekFocus = TextFormat.Duration(TimeSpan.FromTicks(focus.Sum(s => s.Actual.Ticks)));

                Days.Clear();
                foreach (var (day, minutes) in perDay)
                {
                    Days.Add(new FocusDayBar(
                        day.ToString("ddd", PtBr).TrimEnd('.'),
                        minutes / max,
                        $"{day:dd/MM}: {Math.Round(minutes)} min de foco",
                        day == today));
                }

                Sessions.Clear();
                foreach (var session in sessions.Take(50))
                {
                    Sessions.Add(new FocusSessionItem(
                        TitleOf(session),
                        $"{session.StartedAt.ToLocalTime():dd/MM HH:mm} – {session.EndedAt.ToLocalTime():HH:mm}",
                        TextFormat.Duration(session.Actual),
                        session.Completed));
                }

                IsEmpty = sessions.Count == 0;
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Falha ao carregar o histórico de foco.");
        }
    }

    [RelayCommand]
    private async Task ClearAsync()
    {
        if (Confirm?.Invoke("Apagar todo o histórico de foco?") == false)
        {
            return;
        }

        await store.ClearAsync();
        await LoadAsync();
    }

    public void Dispose()
    {
    }

    private static string TitleOf(FocusSession session) => session.Kind switch
    {
        FocusSessionKind.Focus => session.Completed ? "Foco (Pomodoro)" : "Foco interrompido",
        FocusSessionKind.ShortBreak => "Pausa curta",
        FocusSessionKind.LongBreak => "Pausa longa",
        FocusSessionKind.Timer => session.Completed ? "Temporizador" : "Temporizador cancelado",
        _ => "Cronômetro",
    };
}
