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

/// <summary>Linha de um aplicativo no misturador: o slider ajusta só o som dele.</summary>
public sealed partial class AppVolumeItem : ObservableObject
{
    // Depois de um ajuste feito aqui, a leitura periódica não sobrescreve o slider por um instante.
    private static readonly TimeSpan UserChangeHold = TimeSpan.FromSeconds(1.5);

    private readonly IAppVolumeService _service;
    private bool _applying;
    private long _lastUserChange;

    public AppVolumeItem(AppAudioSession session, IAppVolumeService service)
    {
        _service = service;
        Id = session.Id;
        Name = session.Name;
        _level = Math.Round(session.Level * 100);
        _isMuted = session.IsMuted;
    }

    public string Id { get; }

    public string Name { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Percent), nameof(Glyph))]
    private double _level;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Glyph))]
    private bool _isMuted;

    public int Percent => (int)Math.Round(Level);

    public string Glyph => VolumeContent.GlyphFor(IsMuted, Level);

    /// <summary>Reflete o estado lido do Windows sem reenviá-lo.</summary>
    public void Apply(AppAudioSession session)
    {
        if (_lastUserChange != 0 && System.Diagnostics.Stopwatch.GetElapsedTime(_lastUserChange) < UserChangeHold)
        {
            return;
        }

        _applying = true;
        try
        {
            Level = Math.Round(session.Level * 100);
            IsMuted = session.IsMuted;
        }
        finally
        {
            _applying = false;
        }
    }

    [RelayCommand]
    private void ToggleMute()
    {
        _lastUserChange = System.Diagnostics.Stopwatch.GetTimestamp();
        IsMuted = !IsMuted;
        _ = _service.SetMutedAsync(Id, IsMuted);
    }

    partial void OnLevelChanged(double value)
    {
        if (_applying)
        {
            return;
        }

        _lastUserChange = System.Diagnostics.Stopwatch.GetTimestamp();
        if (IsMuted && value > 0)
        {
            IsMuted = false;
            _ = _service.SetMutedAsync(Id, false);
        }

        _ = _service.SetLevelAsync(Id, (float)(value / 100));
    }
}

/// <summary>
/// View model do volume. <see cref="Level"/> é ligado ao slider nos dois sentidos. No painel também
/// mostra as saídas disponíveis (para trocar a padrão) e o microfone padrão.
/// </summary>
public sealed partial class VolumeContent(IAudioVolumeService audio, IAudioDeviceService? devices = null, IAppVolumeService? appVolumes = null) : ObservableObject, IActivityContent
{
    /// <summary>Máximo de aplicativos no painel (os que estão tocando vêm primeiro).</summary>
    public const int MaxApps = 5;

    private static readonly TimeSpan AppsRefreshInterval = TimeSpan.FromSeconds(2.5);

    private bool _applyingSystemState;
    private IslandState? _presentedIn;
    private System.Windows.Threading.DispatcherTimer? _appsTimer;

    /// <summary>Volume de cada aplicativo que está fazendo som (misturador do Windows).</summary>
    public System.Collections.ObjectModel.ObservableCollection<AppVolumeItem> Apps { get; } = [];

    public bool HasApps => Apps.Count > 0;

    /// <summary>A quantidade de linhas mudou com o painel aberto: a ilha precisa recalcular a altura.</summary>
    public event EventHandler? SizeChanged;

    /// <summary>Atualiza a lista de aplicativos sem recriar as linhas existentes. Chamar na thread de UI.</summary>
    public void SetApps(IReadOnlyList<AppAudioSession> sessions)
    {
        var shown = sessions.Take(MaxApps).ToList();
        var before = Apps.Count;
        for (var i = Apps.Count - 1; i >= 0; i--)
        {
            if (shown.All(s => s.Id != Apps[i].Id))
            {
                Apps.RemoveAt(i);
            }
        }

        foreach (var session in shown)
        {
            if (Apps.FirstOrDefault(a => a.Id == session.Id) is { } existing)
            {
                existing.Apply(session);
            }
            else if (appVolumes is not null)
            {
                Apps.Add(new AppVolumeItem(session, appVolumes));
            }
        }

        if (Apps.Count != before)
        {
            OnPropertyChanged(nameof(HasApps));
            if (_presentedIn == IslandState.Interactive)
            {
                SizeChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    /// <summary>Lê os aplicativos agora (antes de abrir o painel, para ele já abrir no tamanho certo).</summary>
    public async Task RefreshAppsAsync()
    {
        if (appVolumes is null)
        {
            return;
        }

        var sessions = await appVolumes.GetSessionsAsync();
        if (System.Windows.Application.Current?.Dispatcher is { } dispatcher)
        {
            await dispatcher.BeginInvoke(() => SetApps(sessions));
        }
        else
        {
            SetApps(sessions);
        }
    }

    /// <summary>Com o painel aberto, acompanha aplicativos que começam ou param de tocar.</summary>
    private void UpdateAppsTimer(bool visible)
    {
        if (visible && _appsTimer is null && appVolumes is not null && System.Windows.Application.Current?.Dispatcher is { } dispatcher)
        {
            _appsTimer = new System.Windows.Threading.DispatcherTimer(System.Windows.Threading.DispatcherPriority.Background, dispatcher)
            {
                Interval = AppsRefreshInterval,
            };
            _appsTimer.Tick += (_, _) => _ = RefreshAppsAsync();
            _appsTimer.Start();
        }
        else if (!visible && _appsTimer is not null)
        {
            _appsTimer.Stop();
            _appsTimer = null;
        }
    }

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
        var opened = state == IslandState.Interactive && _presentedIn != IslandState.Interactive;
        _presentedIn = state;
        if (opened && devices is not null)
        {
            _ = RefreshOutputsAsync();
        }

        if (opened)
        {
            _ = RefreshAppsAsync();
        }

        UpdateAppsTimer(state == IslandState.Interactive);
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
        IslandState.Interactive => new IslandDimensions(380, 104 + (HasMicrophone ? 48 : 0) + (HasDeviceChoice ? 40 : 0) + (HasApps ? 30 + Apps.Count * 32 : 0), 32),
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
