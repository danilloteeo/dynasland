using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dynasland.Core.Activities;

namespace Dynasland.Modules.Water;

/// <summary>Aviso "Hora de beber água" com as ações Bebi e Adiar.</summary>
public sealed partial class WaterReminderContent(Action drink, Action snooze, string message) : ObservableObject, IActivityContent
{
    public string Glyph => "💧";

    public string Title => "Hora de beber água";

    public string Message { get; } = message;

    public string SnoozeLabel => "Adiar 10 min";

    public string DrinkLabel => "Bebi";

    [RelayCommand]
    private void Drink() => drink();

    [RelayCommand]
    private void Snooze() => snooze();
}
