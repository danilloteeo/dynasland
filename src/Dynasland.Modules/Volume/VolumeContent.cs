using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dynasland.Core.Activities;
using Dynasland.Core.Interfaces;
using Dynasland.Core.Layout;
using Dynasland.Core.Models;
using Dynasland.Modules.Common;

namespace Dynasland.Modules.Volume;

/// <summary>View model do volume. <see cref="Level"/> é ligado ao slider nos dois sentidos.</summary>
public sealed partial class VolumeContent(IAudioVolumeService audio) : ObservableObject, IActivityContent
{
    private bool _applyingSystemState;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Percent), nameof(Glyph))]
    private double _level;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Glyph))]
    private bool _isMuted;

    [ObservableProperty]
    private string _deviceName = string.Empty;

    public int Percent => (int)Math.Round(Level);

    public string Glyph => GlyphFor(IsMuted, Level);

    public static string GlyphFor(bool muted, double level) => muted || level <= 0
        ? Glyphs.Mute
        : level < 34 ? Glyphs.VolumeLow : level < 67 ? Glyphs.VolumeMedium : Glyphs.VolumeHigh;

    /// <summary>Reflete o estado do sistema sem reenviá-lo ao Windows. Chamar na thread de UI.</summary>
    public void Apply(VolumeState? state)
    {
        if (state is null)
        {
            return;
        }

        _applyingSystemState = true;
        try
        {
            Level = state.Percent;
            IsMuted = state.IsMuted;
            DeviceName = string.IsNullOrWhiteSpace(state.DeviceName) ? "Saída de áudio" : state.DeviceName;
        }
        finally
        {
            _applyingSystemState = false;
        }
    }

    public IslandDimensions? GetPreferredDimensions(IslandState state) => state switch
    {
        IslandState.Expanded => new IslandDimensions(300, 50, 25),
        IslandState.Interactive => new IslandDimensions(360, 104, 32),
        _ => null,
    };

    [RelayCommand]
    private void ToggleMute() => audio.SetMuted(!IsMuted);

    partial void OnLevelChanged(double value)
    {
        if (_applyingSystemState)
        {
            return;
        }

        // Arrastar o slider com o som mudo também reativa o som, como nas teclas de volume.
        if (IsMuted && value > 0)
        {
            audio.SetMuted(false);
        }

        audio.SetLevel((float)(value / 100));
    }
}
