using System.Runtime.InteropServices;
using Dynasland.Infrastructure.Windows.Audio;

namespace Dynasland.Infrastructure.Windows.Capture;

/// <summary>
/// Captura de áudio WASAPI em modo compartilhado: o que toca na saída padrão (loopback) ou o microfone
/// padrão. Converte para PCM float estéreo em <see cref="SampleRate"/> Hz e acumula num buffer que o
/// gravador consome no ritmo do relógio da gravação.
/// </summary>
internal sealed class AudioCapture : IDisposable
{
    public const int SampleRate = 48000;
    public const int Channels = 2;

    private const int StreamFlagsLoopback = 0x00020000;
    private const long BufferDuration = 2_000_000; // 200 ms em unidades de 100 ns
    private const int BufferFlagSilent = 0x2;

    // Limite do buffer acumulado: mais que isso indica atraso do consumidor; descarta o mais antigo.
    private const int MaxBufferedFrames = SampleRate / 2;

    private static readonly Guid AudioClientIid = new("1CB9AD4C-DBFA-4C32-B178-C2F568A703B2");
    private static readonly Guid CaptureClientIid = new("C8ADBD64-E71E-48A0-A4DE-185C395CD317");
    private static readonly Guid FloatSubFormat = new("00000003-0000-0010-8000-00AA00389B71");

    private readonly Lock _lock = new();
    private readonly List<float> _buffer = [];
    private readonly IAudioClient _client;
    private readonly IAudioCaptureClient _capture;
    private readonly int _sourceRate;
    private readonly int _sourceChannels;
    private readonly bool _isFloat;
    private readonly int _bitsPerSample;
    private readonly Thread _thread;
    private volatile bool _running;
    private double _resamplePosition;
    private float[] _previousFrame;

    private AudioCapture(IAudioClient client, IAudioCaptureClient capture, int rate, int channels, bool isFloat, int bits)
    {
        _client = client;
        _capture = capture;
        _sourceRate = rate;
        _sourceChannels = channels;
        _isFloat = isFloat;
        _bitsPerSample = bits;
        _previousFrame = new float[Channels];
        _thread = new Thread(Loop) { IsBackground = true, Name = "Dynasland.AudioCapture", Priority = ThreadPriority.AboveNormal };
    }

    /// <param name="loopback"><c>true</c>: áudio do sistema; <c>false</c>: microfone.</param>
    /// <returns><c>null</c> se não houver dispositivo.</returns>
    public static AudioCapture? Open(bool loopback)
    {
        var enumerator = (IMMDeviceEnumerator)new MMDeviceEnumeratorComObject();
        try
        {
            var flow = loopback ? EDataFlow.Render : EDataFlow.Capture;
            if (enumerator.GetDefaultAudioEndpoint(flow, ERole.Console, out var device) != 0 || device is null)
            {
                return null;
            }

            try
            {
                var iid = AudioClientIid;
                Marshal.ThrowExceptionForHR(device.Activate(ref iid, CoreAudioNative.ClsCtxAll, IntPtr.Zero, out var instance));
                var client = (IAudioClient)instance;
                Marshal.ThrowExceptionForHR(client.GetMixFormat(out var format));
                try
                {
                    var tag = (ushort)Marshal.ReadInt16(format, 0);
                    var channels = Marshal.ReadInt16(format, 2);
                    var rate = Marshal.ReadInt32(format, 4);
                    var bits = Marshal.ReadInt16(format, 14);
                    var isFloat = tag == 3 || (tag == 0xFFFE && Marshal.PtrToStructure<Guid>(format + 24) == FloatSubFormat);

                    Marshal.ThrowExceptionForHR(client.Initialize(0, loopback ? StreamFlagsLoopback : 0, BufferDuration, 0, format, IntPtr.Zero));
                    var captureIid = CaptureClientIid;
                    Marshal.ThrowExceptionForHR(client.GetService(ref captureIid, out var captureObject));
                    return new AudioCapture(client, (IAudioCaptureClient)captureObject, rate, channels, isFloat, bits);
                }
                finally
                {
                    Marshal.FreeCoTaskMem(format);
                }
            }
            finally
            {
                Marshal.ReleaseComObject(device);
            }
        }
        finally
        {
            Marshal.ReleaseComObject(enumerator);
        }
    }

    public void Start()
    {
        _running = true;
        Marshal.ThrowExceptionForHR(_client.Start());
        _thread.Start();
    }

    /// <summary>Retira até <paramref name="frames"/> quadros (float intercalado estéreo); completa com silêncio.</summary>
    public void Read(float[] destination, int frames, bool mix)
    {
        lock (_lock)
        {
            var available = Math.Min(frames * Channels, _buffer.Count);
            for (var i = 0; i < available; i++)
            {
                destination[i] = mix ? Math.Clamp(destination[i] + _buffer[i], -1f, 1f) : _buffer[i];
            }

            if (!mix)
            {
                Array.Clear(destination, available, frames * Channels - available);
            }

            _buffer.RemoveRange(0, available);
        }
    }

    /// <summary>Descarta o que foi acumulado (ex.: ao retomar de uma pausa).</summary>
    public void Clear()
    {
        lock (_lock)
        {
            _buffer.Clear();
        }
    }

    public void Dispose()
    {
        _running = false;
        if (_thread.IsAlive)
        {
            _thread.Join(500);
        }

        try
        {
            _client.Stop();
        }
        catch (COMException)
        {
            // Dispositivo removido durante a gravação.
        }

        Marshal.ReleaseComObject(_capture);
        Marshal.ReleaseComObject(_client);
    }

    private void Loop()
    {
        while (_running)
        {
            Thread.Sleep(10);
            try
            {
                while (_running && _capture.GetNextPacketSize(out var packet) == 0 && packet > 0)
                {
                    if (_capture.GetBuffer(out var data, out var frames, out var flags, out _, out _) != 0)
                    {
                        break;
                    }

                    Append(data, (int)frames, (flags & BufferFlagSilent) != 0);
                    _capture.ReleaseBuffer(frames);
                }
            }
            catch (COMException)
            {
                // Dispositivo trocado/removido: segue gravando o restante (silêncio).
                return;
            }
        }
    }

    /// <summary>Converte o pacote para float estéreo e reamostra (linear) para 48 kHz.</summary>
    private void Append(IntPtr data, int frames, bool silent)
    {
        var converted = new float[frames * Channels];
        if (!silent)
        {
            var bytesPerSample = _bitsPerSample / 8;
            for (var frame = 0; frame < frames; frame++)
            {
                for (var channel = 0; channel < Channels; channel++)
                {
                    var source = Math.Min(channel, _sourceChannels - 1);
                    var offset = (frame * _sourceChannels + source) * bytesPerSample;
                    converted[frame * Channels + channel] = _isFloat
                        ? BitConverter.Int32BitsToSingle(Marshal.ReadInt32(data, offset))
                        : _bitsPerSample == 16
                            ? Marshal.ReadInt16(data, offset) / 32768f
                            : Marshal.ReadInt32(data, offset) / 2147483648f;
                }
            }
        }

        lock (_lock)
        {
            if (_sourceRate == SampleRate)
            {
                _buffer.AddRange(converted);
            }
            else
            {
                var step = (double)_sourceRate / SampleRate;
                while (_resamplePosition < frames)
                {
                    var index = (int)_resamplePosition;
                    var fraction = (float)(_resamplePosition - index);
                    for (var channel = 0; channel < Channels; channel++)
                    {
                        var a = index == 0 ? _previousFrame[channel] : converted[(index - 1) * Channels + channel];
                        var b = converted[index * Channels + channel];
                        _buffer.Add(a + (b - a) * fraction);
                    }

                    _resamplePosition += step;
                }

                _resamplePosition -= frames;
                Array.Copy(converted, (frames - 1) * Channels, _previousFrame, 0, Channels);
            }

            var excess = _buffer.Count - MaxBufferedFrames * Channels;
            if (excess > 0)
            {
                _buffer.RemoveRange(0, excess);
            }
        }
    }

    [ComImport]
    [Guid("1CB9AD4C-DBFA-4C32-B178-C2F568A703B2")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioClient
    {
        [PreserveSig] int Initialize(int shareMode, int streamFlags, long bufferDuration, long periodicity, IntPtr format, IntPtr sessionGuid);
        [PreserveSig] int GetBufferSize(out uint frames);
        [PreserveSig] int GetStreamLatency(out long latency);
        [PreserveSig] int GetCurrentPadding(out uint padding);
        [PreserveSig] int IsFormatSupported(int shareMode, IntPtr format, out IntPtr closestMatch);
        [PreserveSig] int GetMixFormat(out IntPtr format);
        [PreserveSig] int GetDevicePeriod(out long defaultPeriod, out long minimumPeriod);
        [PreserveSig] int Start();
        [PreserveSig] int Stop();
        [PreserveSig] int Reset();
        [PreserveSig] int SetEventHandle(IntPtr handle);
        [PreserveSig] int GetService(ref Guid iid, [MarshalAs(UnmanagedType.IUnknown)] out object service);
    }

    [ComImport]
    [Guid("C8ADBD64-E71E-48A0-A4DE-185C395CD317")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioCaptureClient
    {
        [PreserveSig] int GetBuffer(out IntPtr data, out uint frames, out int flags, out ulong devicePosition, out ulong qpcPosition);
        [PreserveSig] int ReleaseBuffer(uint frames);
        [PreserveSig] int GetNextPacketSize(out uint frames);
    }
}
