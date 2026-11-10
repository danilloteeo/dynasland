using System.Runtime.InteropServices;
using Dynasland.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace Dynasland.Infrastructure.Windows.Audio;

/// <summary>
/// Dispositivos de áudio (Core Audio): lista de saídas/entradas ativas, troca do dispositivo padrão
/// (via <see cref="IPolicyConfig"/>, ver observação na interface) e nível/mudo do microfone padrão,
/// com notificações do Windows para mudanças de nível e de dispositivo.
/// </summary>
/// <remarks>Mesmas regras do serviço de volume: objetos em MTA (pool) e nada de Core Audio dentro dos callbacks.</remarks>
public sealed class CoreAudioDeviceService(ILogger<CoreAudioDeviceService> logger) : IAudioDeviceService
{
    private const int ErrorNotFound = unchecked((int)0x80070490);

    private static readonly Guid OwnEventContext = Guid.NewGuid();

    private readonly Lock _lock = new();
    private IMMDeviceEnumerator? _enumerator;
    private DeviceCallback? _deviceCallback;
    private IAudioEndpointVolume? _microphone;
    private MicrophoneCallback? _microphoneCallback;
    private string? _microphoneName;

    public MicrophoneState? Microphone { get; private set; }

    public event EventHandler<MicrophoneState?>? MicrophoneChanged;

    public event EventHandler? DevicesChanged;

    public Task StartAsync(CancellationToken cancellationToken = default) => Task.Run(() =>
    {
        lock (_lock)
        {
            _enumerator = (IMMDeviceEnumerator)new MMDeviceEnumeratorComObject();
            _deviceCallback = new DeviceCallback(this);
            Marshal.ThrowExceptionForHR(_enumerator.RegisterEndpointNotificationCallback(_deviceCallback));
            AttachMicrophone();
        }
    }, cancellationToken);

    public Task StopAsync()
    {
        lock (_lock)
        {
            DetachMicrophone();
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

            Microphone = null;
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<AudioDevice>> GetOutputDevicesAsync() => Task.Run(() => List(EDataFlow.Render));

    public Task<IReadOnlyList<AudioDevice>> GetInputDevicesAsync() => Task.Run(() => List(EDataFlow.Capture));

    public Task<bool> SetDefaultDeviceAsync(string deviceId) => Task.Run(() =>
    {
        try
        {
            var policy = (IPolicyConfig)new PolicyConfigClientComObject();
            try
            {
                foreach (var role in new[] { ERole.Console, ERole.Multimedia, ERole.Communications })
                {
                    Marshal.ThrowExceptionForHR(policy.SetDefaultEndpoint(deviceId, role));
                }

                return true;
            }
            finally
            {
                Marshal.ReleaseComObject(policy);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Não foi possível trocar o dispositivo de áudio padrão.");
            return false;
        }
    });

    public void SetMicrophoneLevel(float level)
    {
        lock (_lock)
        {
            var context = OwnEventContext;
            _microphone?.SetMasterVolumeLevelScalar(Math.Clamp(level, 0f, 1f), ref context);
        }
    }

    public void SetMicrophoneMuted(bool muted)
    {
        lock (_lock)
        {
            var context = OwnEventContext;
            _microphone?.SetMute(muted, ref context);
        }
    }

    private IReadOnlyList<AudioDevice> List(EDataFlow flow)
    {
        lock (_lock)
        {
            if (_enumerator is null)
            {
                return [];
            }

            string? defaultId = null;
            if (_enumerator.GetDefaultAudioEndpoint(flow, ERole.Multimedia, out var defaultDevice) == 0 && defaultDevice is not null)
            {
                defaultDevice.GetId(out defaultId);
                Marshal.ReleaseComObject(defaultDevice);
            }

            if (_enumerator.EnumAudioEndpoints(flow, CoreAudioNative.DeviceStateActive, out var collectionPointer) != 0)
            {
                return [];
            }

            var collection = (IMMDeviceCollection)Marshal.GetObjectForIUnknown(collectionPointer);
            Marshal.Release(collectionPointer);
            var devices = new List<AudioDevice>();
            try
            {
                collection.GetCount(out var count);
                for (uint i = 0; i < count; i++)
                {
                    if (collection.Item(i, out var device) != 0 || device is null)
                    {
                        continue;
                    }

                    try
                    {
                        device.GetId(out var id);
                        devices.Add(new AudioDevice(id, ReadFriendlyName(device) ?? "Dispositivo de áudio", id == defaultId));
                    }
                    finally
                    {
                        Marshal.ReleaseComObject(device);
                    }
                }
            }
            finally
            {
                Marshal.ReleaseComObject(collection);
            }

            return [.. devices.OrderBy(d => d.Name, StringComparer.CurrentCultureIgnoreCase)];
        }
    }

    /// <summary>Passa a observar o microfone padrão. Chamar com <see cref="_lock"/>.</summary>
    private void AttachMicrophone()
    {
        DetachMicrophone();
        if (_enumerator is null)
        {
            return;
        }

        var hr = _enumerator.GetDefaultAudioEndpoint(EDataFlow.Capture, ERole.Communications, out var device);
        if (hr == ErrorNotFound || device is null)
        {
            Microphone = null;
            return;
        }

        Marshal.ThrowExceptionForHR(hr);
        try
        {
            _microphoneName = ReadFriendlyName(device);
            var iid = typeof(IAudioEndpointVolume).GUID;
            Marshal.ThrowExceptionForHR(device.Activate(ref iid, CoreAudioNative.ClsCtxAll, IntPtr.Zero, out var instance));
            _microphone = (IAudioEndpointVolume)instance;
            _microphoneCallback = new MicrophoneCallback(this);
            Marshal.ThrowExceptionForHR(_microphone.RegisterControlChangeNotify(_microphoneCallback));
            _microphone.GetMasterVolumeLevelScalar(out var level);
            _microphone.GetMute(out var muted);
            Microphone = new MicrophoneState(level, muted, _microphoneName);
        }
        finally
        {
            Marshal.ReleaseComObject(device);
        }
    }

    private void DetachMicrophone()
    {
        if (_microphone is null)
        {
            return;
        }

        if (_microphoneCallback is not null)
        {
            _microphone.UnregisterControlChangeNotify(_microphoneCallback);
        }

        Marshal.ReleaseComObject(_microphone);
        _microphone = null;
        _microphoneCallback = null;
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

    private void OnMicrophoneNotification(IntPtr data)
    {
        var notification = Marshal.PtrToStructure<AudioVolumeNotificationData>(data);
        var state = new MicrophoneState(notification.MasterVolume, notification.Muted != 0, _microphoneName);
        Microphone = state;
        MicrophoneChanged?.Invoke(this, state);
    }

    private void LogMicrophoneFailure(Exception ex) => logger.LogError(ex, "Falha ao processar notificação do microfone.");

    private void OnDevicesChanged(bool captureDefaultChanged) => Task.Run(() =>
    {
        try
        {
            if (captureDefaultChanged)
            {
                MicrophoneState? state;
                lock (_lock)
                {
                    if (_enumerator is null)
                    {
                        return;
                    }

                    AttachMicrophone();
                    state = Microphone;
                }

                MicrophoneChanged?.Invoke(this, state);
            }

            DevicesChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Falha ao acompanhar a mudança de dispositivos de áudio.");
        }
    });

    private sealed class MicrophoneCallback(CoreAudioDeviceService owner) : IAudioEndpointVolumeCallback
    {
        public int OnNotify(IntPtr notifyData)
        {
            try
            {
                owner.OnMicrophoneNotification(notifyData);
            }
            catch (Exception ex)
            {
                owner.LogMicrophoneFailure(ex);
            }

            return 0;
        }
    }

    private sealed class DeviceCallback(CoreAudioDeviceService owner) : IMMNotificationClient
    {
        public int OnDeviceStateChanged(string deviceId, int newState)
        {
            owner.OnDevicesChanged(captureDefaultChanged: false);
            return 0;
        }

        public int OnDeviceAdded(string deviceId) => 0;

        public int OnDeviceRemoved(string deviceId) => 0;

        public int OnDefaultDeviceChanged(EDataFlow flow, ERole role, string? defaultDeviceId)
        {
            // Cada troca dispara uma notificação por função; reage a uma só.
            if (role == ERole.Multimedia || (flow == EDataFlow.Capture && role == ERole.Communications))
            {
                owner.OnDevicesChanged(captureDefaultChanged: flow == EDataFlow.Capture);
            }

            return 0;
        }

        public int OnPropertyValueChanged(string deviceId, PropertyKey key) => 0;
    }
}
