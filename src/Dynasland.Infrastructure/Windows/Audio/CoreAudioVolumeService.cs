using System.Runtime.InteropServices;
using Dynasland.Core.Interfaces;
using Dynasland.Core.Models;
using Microsoft.Extensions.Logging;

namespace Dynasland.Infrastructure.Windows.Audio;

/// <summary>
/// Volume do dispositivo de saída padrão via Core Audio (IAudioEndpointVolume).
/// Notificado pelo Windows a cada alteração; acompanha a troca do dispositivo padrão.
/// </summary>
/// <remarks>
/// Os objetos COM são criados numa thread MTA (pool) e o Windows chama os callbacks em threads próprias.
/// A documentação proíbe chamar Core Audio de dentro de OnDefaultDeviceChanged, por isso a troca de
/// dispositivo é agendada no pool.
/// </remarks>
public sealed class CoreAudioVolumeService(ILogger<CoreAudioVolumeService> logger) : IAudioVolumeService
{
    private const int ErrorNotFound = unchecked((int)0x80070490);

    // Contexto enviado nas alterações feitas pelo Dynasland, para distingui-las das externas.
    private static readonly Guid OwnEventContext = Guid.NewGuid();

    private readonly Lock _lock = new();
    private IMMDeviceEnumerator? _enumerator;
    private IAudioEndpointVolume? _endpoint;
    private VolumeCallback? _volumeCallback;
    private DeviceCallback? _deviceCallback;
    private string? _deviceName;

    public VolumeState? Current { get; private set; }

    public event EventHandler<VolumeChangedEventArgs>? Changed;

    public Task<bool> StartAsync(CancellationToken cancellationToken = default) => Task.Run(() =>
    {
        lock (_lock)
        {
            _enumerator = (IMMDeviceEnumerator)new MMDeviceEnumeratorComObject();
            _deviceCallback = new DeviceCallback(this);
            Marshal.ThrowExceptionForHR(_enumerator.RegisterEndpointNotificationCallback(_deviceCallback));
            return AttachDefaultDevice();
        }
    }, cancellationToken);

    public Task StopAsync()
    {
        lock (_lock)
        {
            DetachEndpoint();
            if (_enumerator is not null)
            {
                if (_deviceCallback is not null)
                {
                    _enumerator.UnregisterEndpointNotificationCallback(_deviceCallback);
                }

                Marshal.ReleaseComObject(_enumerator);
                _enumerator = null;
                _deviceCallback = null;
            }

            Current = null;
        }

        return Task.CompletedTask;
    }

    public void SetLevel(float level)
    {
        lock (_lock)
        {
            var context = OwnEventContext;
            _endpoint?.SetMasterVolumeLevelScalar(Math.Clamp(level, 0f, 1f), ref context);
        }
    }

    public void SetMuted(bool muted)
    {
        lock (_lock)
        {
            var context = OwnEventContext;
            _endpoint?.SetMute(muted, ref context);
        }
    }

    /// <summary>Passa a observar o dispositivo padrão. Chamar com <see cref="_lock"/> adquirido.</summary>
    private bool AttachDefaultDevice()
    {
        DetachEndpoint();
        if (_enumerator is null)
        {
            return false;
        }

        var hr = _enumerator.GetDefaultAudioEndpoint(EDataFlow.Render, ERole.Multimedia, out var device);
        if (hr == ErrorNotFound || device is null)
        {
            Current = null;
            return false;
        }

        Marshal.ThrowExceptionForHR(hr);
        try
        {
            _deviceName = ReadFriendlyName(device);

            var iid = typeof(IAudioEndpointVolume).GUID;
            Marshal.ThrowExceptionForHR(device.Activate(ref iid, CoreAudioNative.ClsCtxAll, IntPtr.Zero, out var instance));
            _endpoint = (IAudioEndpointVolume)instance;
            _volumeCallback = new VolumeCallback(this);
            Marshal.ThrowExceptionForHR(_endpoint.RegisterControlChangeNotify(_volumeCallback));

            _endpoint.GetMasterVolumeLevelScalar(out var level);
            _endpoint.GetMute(out var muted);
            Current = new VolumeState(level, muted, _deviceName);
            return true;
        }
        finally
        {
            Marshal.ReleaseComObject(device);
        }
    }

    private void DetachEndpoint()
    {
        if (_endpoint is null)
        {
            return;
        }

        if (_volumeCallback is not null)
        {
            _endpoint.UnregisterControlChangeNotify(_volumeCallback);
        }

        Marshal.ReleaseComObject(_endpoint);
        _endpoint = null;
        _volumeCallback = null;
    }

    private static string? ReadFriendlyName(IMMDevice device)
    {
        if (device.OpenPropertyStore(CoreAudioNative.StgmRead, out var store) != 0 || store is null)
        {
            return null;
        }

        try
        {
            var key = CoreAudioNative.DeviceFriendlyName;
            if (store.GetValue(ref key, out var value) != 0)
            {
                return null;
            }

            try
            {
                return value.VarType == PropVariant.VT_LPWSTR ? Marshal.PtrToStringUni(value.Pointer) : null;
            }
            finally
            {
                CoreAudioNative.PropVariantClear(ref value);
            }
        }
        finally
        {
            Marshal.ReleaseComObject(store);
        }
    }

    private void OnVolumeNotification(IntPtr data)
    {
        var notification = Marshal.PtrToStructure<AudioVolumeNotificationData>(data);
        var state = new VolumeState(notification.MasterVolume, notification.Muted != 0, _deviceName);
        Current = state;

        var reason = notification.EventContext == OwnEventContext ? VolumeChangeReason.Own : VolumeChangeReason.External;
        Changed?.Invoke(this, new VolumeChangedEventArgs(state, reason));
    }

    private void LogNotificationFailure(Exception ex) => logger.LogError(ex, "Falha ao processar notificação de volume.");

    private void OnDefaultDeviceChanged() => Task.Run(() =>
    {
        try
        {
            VolumeState? state;
            lock (_lock)
            {
                if (_enumerator is null)
                {
                    return;
                }

                AttachDefaultDevice();
                state = Current;
            }

            if (state is not null)
            {
                Changed?.Invoke(this, new VolumeChangedEventArgs(state, VolumeChangeReason.DeviceChanged));
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Falha ao acompanhar a troca do dispositivo de áudio.");
        }
    });

    private sealed class VolumeCallback(CoreAudioVolumeService owner) : IAudioEndpointVolumeCallback
    {
        public int OnNotify(IntPtr notifyData)
        {
            try
            {
                owner.OnVolumeNotification(notifyData);
            }
            catch (Exception ex)
            {
                owner.LogNotificationFailure(ex);
            }

            return 0;
        }
    }

    private sealed class DeviceCallback(CoreAudioVolumeService owner) : IMMNotificationClient
    {
        public int OnDeviceStateChanged(string deviceId, int newState) => 0;

        public int OnDeviceAdded(string deviceId) => 0;

        public int OnDeviceRemoved(string deviceId) => 0;

        public int OnDefaultDeviceChanged(EDataFlow flow, ERole role, string? defaultDeviceId)
        {
            if (flow == EDataFlow.Render && role == ERole.Multimedia)
            {
                owner.OnDefaultDeviceChanged();
            }

            return 0;
        }

        public int OnPropertyValueChanged(string deviceId, PropertyKey key) => 0;
    }
}
