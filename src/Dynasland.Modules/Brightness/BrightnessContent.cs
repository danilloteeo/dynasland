using CommunityToolkit.Mvvm.ComponentModel;
using Dynasland.Core.Activities;
using Dynasland.Core.Layout;
using Dynasland.Core.Models;
using Dynasland.Modules.Common;

namespace Dynasland.Modules.Brightness;

/// <summary>View model do brilho. Alterações do slider são repassadas por <paramref name="requestBrightness"/>.</summary>
public sealed partial class BrightnessContent(Action<int> requestBrightness) : ObservableObject, IActivityContent
{
    private bool _applyingSystemState;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Percent))]
    private double _level;

    public int Percent => (int)Math.Round(Level);

    public string Glyph => Glyphs.Brightness;

    /// <summary>Nome da tela (ex.: "Tela integrada", "Monitor 2").</summary>
    public string Name { get; init; } = "Brilho";

    public string DisplayId { get; init; } = BrightnessModule.IntegratedId;

    public void Apply(int percent)
    {
        _applyingSystemState = true;
        try
        {
            Level = Math.Clamp(percent, 0, 100);
        }
        finally
        {
            _applyingSystemState = false;
        }
    }

    public IslandDimensions? GetPreferredDimensions(IslandState state) => state switch
    {
        IslandState.Expanded => new IslandDimensions(300, 50, 25),
        IslandState.Interactive => new IslandDimensions(440, 96, 32),
        _ => null,
    };

    partial void OnLevelChanged(double value)
    {
        if (!_applyingSystemState)
        {
            requestBrightness((int)Math.Round(value));
        }
    }
}

/// <summary>Painel do brilho: um controle por tela (integrada e monitores externos com DDC/CI).</summary>
public sealed partial class BrightnessPanelContent(Func<Task> refresh) : ObservableObject, IActivityContent
{
    public System.Collections.ObjectModel.ObservableCollection<BrightnessContent> Displays { get; } = [];

    public string Glyph => Glyphs.Brightness;

    /// <summary>Atualiza a lista mantendo os controles existentes (evita "pular" o slider em uso).</summary>
    public void SetDisplays(BrightnessContent? integrated, IReadOnlyList<Dynasland.Core.Interfaces.ExternalDisplayBrightness> external, Func<string, Action<int>> request)
    {
        var wanted = new List<BrightnessContent>();
        if (integrated is not null)
        {
            wanted.Add(integrated);
        }

        foreach (var display in external)
        {
            var existing = Displays.FirstOrDefault(d => d.DisplayId == display.Id);
            var content = existing ?? new BrightnessContent(request(display.Id)) { Name = display.Name, DisplayId = display.Id };
            content.Apply(display.Percent);
            wanted.Add(content);
        }

        Displays.Clear();
        foreach (var content in wanted)
        {
            Displays.Add(content);
        }
    }

    public IslandDimensions? GetPreferredDimensions(IslandState state) =>
        state == IslandState.Interactive ? new IslandDimensions(380, 44 + Math.Max(1, Displays.Count) * 54, 32) : null;

    public void OnPresentationChanged(IslandState? state)
    {
        if (state == IslandState.Interactive)
        {
            _ = refresh();
        }
    }
}
