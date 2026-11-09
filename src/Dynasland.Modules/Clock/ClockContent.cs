using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dynasland.Core.Activities;
using Dynasland.Core.Layout;
using Dynasland.Core.Models;
using Dynasland.Core.Modules;

namespace Dynasland.Modules.Clock;

public sealed record ShortcutViewModel(string Glyph, string Label, IRelayCommand Command);

/// <summary>Tela inicial da ilha: hora, data e atalhos dos módulos ativos.</summary>
public sealed partial class ClockContent : ObservableObject, IActivityContent
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    [ObservableProperty]
    private string _timeText = string.Empty;

    [ObservableProperty]
    private string _dateText = string.Empty;

    public ObservableCollection<ShortcutViewModel> Shortcuts { get; } = [];

    public bool HasShortcuts => Shortcuts.Count > 0;

    public void Update(DateTimeOffset localNow)
    {
        TimeText = localNow.ToString("HH:mm", PtBr);
        var date = localNow.ToString("dddd, d 'de' MMMM", PtBr);
        DateText = char.ToUpper(date[0], PtBr) + date[1..];
    }

    public void SetShortcuts(IEnumerable<IslandShortcut> shortcuts)
    {
        Shortcuts.Clear();
        foreach (var shortcut in shortcuts)
        {
            Shortcuts.Add(new ShortcutViewModel(shortcut.Glyph, shortcut.Label, new RelayCommand(shortcut.Invoke)));
        }

        OnPropertyChanged(nameof(HasShortcuts));
    }

    public IslandDimensions? GetPreferredDimensions(IslandState state) =>
        state == IslandState.Interactive && HasShortcuts ? new IslandDimensions(400, 168, 36) : null;
}
