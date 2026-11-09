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
        IslandState.Interactive => new IslandDimensions(360, 96, 32),
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
