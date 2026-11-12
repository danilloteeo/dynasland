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

    private IdleDisplay _idleDisplay = IdleDisplay.DateAndTime;
    private IslandGlance? _idleGlance;

    /// <summary>Em repouso a ilha mostra algo (desligado em <see cref="IdleDisplay.Nothing"/>).</summary>
    public bool IsIdleVisible => _idleDisplay != IdleDisplay.Nothing;

    /// <summary>Só a hora, centralizada.</summary>
    public bool IsIdleTimeOnly => _idleDisplay == IdleDisplay.TimeOnly;

    /// <summary>Ícone da informação à esquerda da hora (clima, compromisso); vazio para a data.</summary>
    public string IdleGlyph => _idleGlance?.Glyph ?? string.Empty;

    public bool HasIdleGlyph => IdleGlyph.Length > 0;

    /// <summary>Texto à esquerda da hora: a informação escolhida ou, na falta dela, a data.</summary>
    public string IdleText => _idleGlance is { } glance ? glance.ShortText ?? glance.Text : ShortDateText;

    /// <summary>Define o que aparece em repouso, a partir da preferência e das informações disponíveis.</summary>
    public void SetIdle(IdleDisplay display, IEnumerable<IslandGlance> glances)
    {
        var glanceId = display switch
        {
            IdleDisplay.Weather => Life.WeatherModule.GlanceId,
            IdleDisplay.NextEvent => Life.CalendarModule.GlanceId,
            IdleDisplay.Network => SystemMonitor.SystemMonitorModule.NetworkGlanceId,
            _ => null,
        };

        _idleDisplay = display;
        _idleGlance = glanceId is null ? null : glances.FirstOrDefault(g => g.Id == glanceId);
        OnPropertyChanged(nameof(IsIdleVisible));
        OnPropertyChanged(nameof(IsIdleTimeOnly));
        OnPropertyChanged(nameof(IdleGlyph));
        OnPropertyChanged(nameof(HasIdleGlyph));
        OnPropertyChanged(nameof(IdleText));
    }

    partial void OnShortDateTextChanged(string value) => OnPropertyChanged(nameof(IdleText));

    /// <summary>Atalhos por linha no painel (o restante quebra para a linha seguinte).</summary>
    public const int ShortcutsPerRow = 7;

    public ObservableCollection<ShortcutViewModel> Shortcuts { get; } = [];

    public ObservableCollection<GlanceViewModel> Glances { get; } = [];

    public bool HasShortcuts => Shortcuts.Count > 0;

    public bool HasGlances => Glances.Count > 0;

    private IslandGlance[] _glanceSource = [];

    public void SetGlances(IEnumerable<IslandGlance> glances)
    {
        // Informações só da ilha em repouso (ex.: rede, que muda a cada 2 s) não entram nem refazem a lista.
        var visible = glances.Where(g => !g.IdleOnly).ToArray();
        if (visible.SequenceEqual(_glanceSource))
        {
            return;
        }

        _glanceSource = visible;
        Glances.Clear();
        foreach (var glance in visible)
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
        return new IslandDimensions(440, 120 + rows * 46 + Glances.Count * 22, 36);
    }
}
