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

/// <summary>Linha de informação rápida (clima, próximo compromisso) abaixo da data.</summary>
public sealed record GlanceViewModel(string Glyph, string Text, IRelayCommand? Command)
{
    public bool IsClickable => Command is not null;
}

/// <summary>Tela inicial da ilha: hora, data e atalhos dos módulos ativos.</summary>
public sealed partial class ClockContent : ObservableObject, IActivityContent
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    [ObservableProperty]
    private string _timeText = string.Empty;

    [ObservableProperty]
    private string _dateText = string.Empty;

    /// <summary>Data abreviada ("seg, 9 nov") para a ilha compacta em repouso.</summary>
    [ObservableProperty]
    private string _shortDateText = string.Empty;

    /// <summary>Atalhos por linha no painel (o restante quebra para a linha seguinte).</summary>
    public const int ShortcutsPerRow = 7;

    public ObservableCollection<ShortcutViewModel> Shortcuts { get; } = [];

    public ObservableCollection<GlanceViewModel> Glances { get; } = [];

    public bool HasShortcuts => Shortcuts.Count > 0;

    public bool HasGlances => Glances.Count > 0;

    public void SetGlances(IEnumerable<IslandGlance> glances)
    {
        Glances.Clear();
        foreach (var glance in glances)
        {
            Glances.Add(new GlanceViewModel(glance.Glyph, glance.Text, glance.Invoke is { } invoke ? new RelayCommand(invoke) : null));
        }

        OnPropertyChanged(nameof(HasGlances));
    }

    public void Update(DateTimeOffset localNow)
    {
        TimeText = localNow.ToString("HH:mm", PtBr);
        var date = localNow.ToString("dddd, d 'de' MMMM", PtBr);
        DateText = char.ToUpper(date[0], PtBr) + date[1..];
        // As abreviações do pt-BR vêm com ponto ("seg.", "nov.").
        ShortDateText = localNow.ToString("ddd, d MMM", PtBr).Replace(".", string.Empty);
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

    public IslandDimensions? GetPreferredDimensions(IslandState state)
    {
        if (state != IslandState.Interactive || (!HasShortcuts && !HasGlances))
        {
            return null;
        }

        var rows = (Shortcuts.Count + ShortcutsPerRow - 1) / ShortcutsPerRow;
        return new IslandDimensions(400, 120 + rows * 46 + Glances.Count * 22, 36);
    }
}
