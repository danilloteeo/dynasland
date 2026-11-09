using System.Globalization;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dynasland.Core.Interfaces;
using Dynasland.Core.Models;

namespace Dynasland.App.ViewModels;

public sealed partial class IslandViewModel : ObservableObject, IDisposable
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    private readonly IIslandSettingsService _settings;
    private readonly DispatcherTimer _clockTimer;
    private readonly DispatcherTimer _collapseTimer;
    private bool _isPointerOver;

    [ObservableProperty]
    private IslandState _state = IslandState.Compact;

    [ObservableProperty]
    private string _timeText = string.Empty;

    [ObservableProperty]
    private string _dateText = string.Empty;

    public IslandViewModel(IIslandSettingsService settings)
    {
        _settings = settings;

        _clockTimer = new DispatcherTimer(DispatcherPriority.Background);
        _clockTimer.Tick += (_, _) => UpdateClock();

        _collapseTimer = new DispatcherTimer(DispatcherPriority.Background);
        _collapseTimer.Tick += (_, _) =>
        {
            _collapseTimer.Stop();
            State = IslandState.Compact;
        };
    }

    [RelayCommand]
    private void ToggleInteractive() =>
        State = State == IslandState.Interactive ? IslandState.Compact : IslandState.Interactive;

    public void OnPointerEntered()
    {
        _isPointerOver = true;
        _collapseTimer.Stop();
    }

    public void OnPointerExited()
    {
        _isPointerOver = false;
        if (State != IslandState.Compact)
        {
            RestartCollapseTimer();
        }
    }

    partial void OnStateChanged(IslandState value)
    {
        if (value == IslandState.Compact)
        {
            // O relógio só é exibido expandido; compacto não consome timers.
            _clockTimer.Stop();
            _collapseTimer.Stop();
            return;
        }

        UpdateClock();
        if (!_isPointerOver)
        {
            RestartCollapseTimer();
        }
    }

    private void UpdateClock()
    {
        var now = DateTime.Now;
        TimeText = now.ToString("HH:mm", PtBr);
        var date = now.ToString("dddd, d 'de' MMMM", PtBr);
        DateText = char.ToUpper(date[0], PtBr) + date[1..];

        // Próximo tick na virada do minuto: uma atualização por minuto em vez de polling.
        _clockTimer.Stop();
        _clockTimer.Interval = TimeSpan.FromMilliseconds((60 - now.Second) * 1000 - now.Millisecond + 50);
        _clockTimer.Start();
    }

    private void RestartCollapseTimer()
    {
        _collapseTimer.Stop();
        _collapseTimer.Interval = TimeSpan.FromSeconds(_settings.Current.ExpandedDurationSeconds);
        _collapseTimer.Start();
    }

    public void Dispose()
    {
        _clockTimer.Stop();
        _collapseTimer.Stop();
    }
}
