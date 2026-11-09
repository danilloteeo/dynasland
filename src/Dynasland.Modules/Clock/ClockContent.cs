using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using Dynasland.Core.Activities;

namespace Dynasland.Modules.Clock;

public sealed partial class ClockContent : ObservableObject, IActivityContent
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    [ObservableProperty]
    private string _timeText = string.Empty;

    [ObservableProperty]
    private string _dateText = string.Empty;

    public void Update(DateTimeOffset localNow)
    {
        TimeText = localNow.ToString("HH:mm", PtBr);
        var date = localNow.ToString("dddd, d 'de' MMMM", PtBr);
        DateText = char.ToUpper(date[0], PtBr) + date[1..];
    }
}
