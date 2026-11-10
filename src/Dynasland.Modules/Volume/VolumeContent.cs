using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dynasland.Core.Activities;
using Dynasland.Core.Interfaces;
using Dynasland.Core.Layout;
using Dynasland.Core.Models;
using Dynasland.Modules.Common;

namespace Dynasland.Modules.Volume;

/// <summary>Dispositivo de saída na lista do painel; clicar o torna padrão.</summary>
public sealed record AudioDeviceItem(string Name, bool IsDefault, IRelayCommand Select);

/// <summary>
/// View model do volume. <see cref="Level"/> é ligado ao slider nos dois sentidos. No painel também
/// mostra as saídas disponíveis (para trocar a padrão) e o microfone padrão.
/// </summary>
public sealed partial class VolumeContent(IAudioVolumeService audio, IAudioDeviceService? devices = null) : ObservableObject, IActivityContent
{
    private bool _applyingSystemState;

    public System.Collections.ObjectModel.ObservableCollection<AudioDeviceItem> Outputs { get; } = [];

    public bool HasDeviceChoice => Outputs.Count > 1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MicrophonePercent), nameof(MicrophoneGlyph))]
    private double _microphoneLevel;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MicrophoneGlyph))]
    private bool _microphoneMuted;

    [ObservableProperty]
    private bool _hasMicrophone;

    [ObservableProperty]
    private string _microphoneName = string.Empty;

    public int MicrophonePercent => (int)Math.Round(MicrophoneLevel);

    public string MicrophoneGlyph => MicrophoneMuted ? "\uEC54" : Glyphs.Microphone;

    public string SpeakersGlyph => Glyphs.Speakers;

    /// <summary>Reflete o microfone do sistema. Chamar na thread de UI.</summary>
    public void ApplyMicrophone(MicrophoneState? state)
    {
        _applyingSystemState = true;
        try
        {
            HasMicrophone = state is not null;
            if (state is not null)
            {
                MicrophoneLevel = Math.Round(state.Level * 100);
                MicrophoneMuted = state.IsMuted;
                MicrophoneName = string.IsNullOrWhiteSpace(state.DeviceName) ? "Microfone" : state.DeviceName;
            }
        }
        finally
        {
            _applyingSystemState = false;
        }
    }

    /// <summary>Atualiza a lista de saídas. Chamar na thread de UI.</summary>
    public void SetOutputs(IReadOnlyList<AudioDevice> outputs)
    {
        Outputs.Clear();
        foreach (var output in outputs)
        {
            Outputs.Add(new AudioDeviceItem(output.Name, output.IsDefault, new RelayCommand(() => _ = devices?.SetDefaultDeviceAsync(output.Id))));
        }

        OnPropertyChanged(nameof(HasDeviceChoice));
    }

    public void OnPresentationChanged(IslandState? state)
    {
        if (state == IslandState.Interactive && devices is not null)
        {
            _ = RefreshOutputsAsync();
        }
    }

    private async Task RefreshOutputsAsync()
    {
        var outputs = await devices!.GetOutputDevicesAsync();
        System.Windows.Application.Current?.Dispatcher.BeginInvoke(() => SetOutputs(outputs));
    }

    [RelayCommand]
    private void ToggleMicrophoneMute() => devices?.SetMicrophoneMuted(!MicrophoneMuted);

    partial void OnMicrophoneLevelChanged(double value)
    {
        if (!_applyingSystemState)
        {
            devices?.SetMicrophoneLevel((float)(value / 100));
        }
    }

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
        IslandState.Interactive => new IslandDimensions(380, 104 + (HasMicrophone ? 48 : 0) + (HasDeviceChoice ? 40 : 0), 32),
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
