using System.Diagnostics;
using System.Runtime.InteropServices.WindowsRuntime;
using Dynasland.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Windows.Graphics;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;
using Windows.Graphics.DirectX.Direct3D11;
using Windows.Media.Core;
using Windows.Media.MediaProperties;
using Windows.Media.Transcoding;
using Windows.Storage;
using Windows.Storage.Streams;

namespace Dynasland.Infrastructure.Windows.Capture;

/// <summary>
/// Grava um monitor ou uma janela em MP4 (H.264 + AAC). Os quadros vêm do Windows.Graphics.Capture
/// (superfícies Direct3D, sem cópia para a memória) e alimentam um <see cref="MediaStreamSource"/>
/// codificado pelo <see cref="MediaTranscoder"/> do Windows (com aceleração de hardware quando houver).
/// </summary>
/// <remarks>
/// Um relógio único (sem o tempo pausado) dá o carimbo de tempo de vídeo e áudio. O áudio é entregue em
/// blocos de 20 ms no ritmo desse relógio, completado com silêncio quando nada toca (o loopback do Windows
/// não envia pacotes no silêncio), para o vídeo nunca esperar pelo áudio. Conteúdo protegido (DRM) sai
/// preto por decisão do próprio Windows.
/// </remarks>
public sealed class ScreenRecorder(IMonitorService monitors, ILogger<ScreenRecorder> logger) : IScreenRecorder
{
    private const int AudioChunkFrames = AudioCapture.SampleRate / 50; // 20 ms

    private readonly Lock _lock = new();
    private readonly Stopwatch _clock = new();
    private readonly SemaphoreSlim _frameArrived = new(0, int.MaxValue);
    private readonly ManualResetEventSlim _running = new(true);
    private RecordingState _state;
    private Session? _session;

    public bool IsSupported
    {
        get
        {
            try
            {
                return GraphicsCaptureSession.IsSupported();
            }
            catch (Exception)
            {
                return false;
            }
        }
    }

    public RecordingState State
    {
        get
        {
            lock (_lock)
            {
                return _state;
            }
        }
    }

    public TimeSpan Elapsed => _clock.Elapsed;

    public event EventHandler<RecordingState>? StateChanged;

    public event EventHandler<RecordingResult>? Completed;

    public async Task StartAsync(RecordingOptions options, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            if (_state != RecordingState.Idle)
            {
                throw new InvalidOperationException("Já existe uma gravação em andamento.");
            }

            _state = RecordingState.Starting;
        }

        StateChanged?.Invoke(this, RecordingState.Starting);
        try
        {
            if (!IsSupported)
            {
                throw new InvalidOperationException("A gravação de tela exige o Windows 10 versão 1903 ou mais recente.");
            }

            var session = await Task.Run(() => CreateSession(options), cancellationToken);
            _session = session;
            _clock.Restart();
            _running.Set();
            session.Start();
            SetState(RecordingState.Recording);
            _ = RunTranscodeAsync(session);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Não foi possível iniciar a gravação.");
            _session?.Dispose();
            _session = null;
            SetState(RecordingState.Idle);
            throw ex as InvalidOperationException ?? new InvalidOperationException($"Não foi possível iniciar a gravação: {ex.Message}", ex);
        }
    }

    public void Pause()
    {
        if (State != RecordingState.Recording)
        {
            return;
        }

        _clock.Stop();
        _running.Reset();
        SetState(RecordingState.Paused);
    }

    public void Resume()
    {
        if (State != RecordingState.Paused)
        {
            return;
        }

        _session?.ClearAudio();
        _clock.Start();
        _running.Set();
        SetState(RecordingState.Recording);
    }

    public Task StopAsync()
    {
        if (State is not (RecordingState.Recording or RecordingState.Paused))
        {
            return Task.CompletedTask;
        }

        SetState(RecordingState.Stopping);
        _session?.RequestStop();
        _running.Set();
        _frameArrived.Release();
        return Task.CompletedTask;
    }

    private void LogSampleFailure(Exception ex) => logger.LogError(ex, "Falha ao produzir amostra da gravação.");

    private void SetState(RecordingState state)
    {
        lock (_lock)
        {
            _state = state;
        }

        StateChanged?.Invoke(this, state);
    }

    private Session CreateSession(RecordingOptions options)
    {
        var item = options.Target.Kind == RecordingTargetKind.Window
            ? CaptureInterop.CreateItemForWindow(options.Target.Window)
            : CaptureInterop.CreateItemForMonitor(MonitorHandle(options.Target.MonitorDeviceName));

        // H.264 exige dimensões pares.
        var size = new SizeInt32 { Width = item.Size.Width & ~1, Height = item.Size.Height & ~1 };
        if (size.Width < 16 || size.Height < 16)
        {
            throw new InvalidOperationException("A área a gravar é pequena demais (janela minimizada?).");
        }

        var device = CaptureInterop.CreateDevice();
        var system = options.SystemAudio ? AudioCapture.Open(loopback: true) : null;
        var microphone = options.Microphone ? AudioCapture.Open(loopback: false) : null;
        return new Session(this, options, item, device, size, system, microphone);
    }

    private IntPtr MonitorHandle(string? deviceName)
    {
        var all = monitors.GetMonitors();
        var monitor = all.FirstOrDefault(m => string.Equals(m.DeviceName, deviceName, StringComparison.OrdinalIgnoreCase))
            ?? all.FirstOrDefault(m => m.IsPrimary)
            ?? all.FirstOrDefault();
        return monitor is null ? CaptureInterop.MonitorAt(0, 0) : CaptureInterop.MonitorAt(monitor.Bounds.Left, monitor.Bounds.Top);
    }

    private async Task RunTranscodeAsync(Session session)
    {
        RecordingResult result;
        try
        {
            await session.TranscodeAsync();
            var size = new FileInfo(session.Options.OutputPath).Length;
            result = new RecordingResult(true, session.Options.OutputPath, _clock.Elapsed, size, null);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Falha na gravação.");
            result = new RecordingResult(false, File.Exists(session.Options.OutputPath) ? session.Options.OutputPath : null, _clock.Elapsed, 0, ex.Message);
        }
        finally
        {
            _clock.Stop();
            session.Dispose();
            _session = null;
            SetState(RecordingState.Idle);
        }

        Completed?.Invoke(this, result);
    }

    /// <summary>Uma gravação: captura, fonte de mídia e codificação.</summary>
    private sealed class Session : IDisposable
    {
        private readonly ScreenRecorder _owner;
        private readonly GraphicsCaptureItem _item;
        private readonly IDirect3DDevice _device;
        private readonly SizeInt32 _size;
        private readonly AudioCapture? _system;
        private readonly AudioCapture? _microphone;
        private readonly Direct3D11CaptureFramePool _pool;
        private readonly GraphicsCaptureSession _capture;
        private readonly MediaStreamSource _source;
        private readonly VideoStreamDescriptor _videoStream;
        private readonly AudioStreamDescriptor? _audioStream;
        private readonly TimeSpan _frameInterval;
        private readonly Lock _frameLock = new();
        private readonly Dictionary<Direct3D11CaptureFrame, int> _inUse = [];
        private Direct3D11CaptureFrame? _latest;
        private long _latestVersion;
        private long _sentVersion = -1;
        private TimeSpan _lastVideoTime = TimeSpan.MinValue;
        private long _audioChunks;
        private volatile bool _stopRequested;

        public Session(ScreenRecorder owner, RecordingOptions options, GraphicsCaptureItem item, IDirect3DDevice device, SizeInt32 size, AudioCapture? system, AudioCapture? microphone)
        {
            _owner = owner;
            Options = options;
            _item = item;
            _device = device;
            _size = size;
            _system = system;
            _microphone = microphone;
            _frameInterval = TimeSpan.FromSeconds(1.0 / Math.Clamp(options.FramesPerSecond, 10, 60));

            _pool = Direct3D11CaptureFramePool.CreateFreeThreaded(device, DirectXPixelFormat.B8G8R8A8UIntNormalized, 3, size);
            _pool.FrameArrived += OnFrameArrived;
            _capture = _pool.CreateCaptureSession(item);
            if (OperatingSystem.IsWindowsVersionAtLeast(10, 0, 19041))
            {
                _capture.IsCursorCaptureEnabled = options.ShowCursor; // antes do Windows 10 2004 o cursor sempre aparece
            }
            item.Closed += (_, _) => RequestStop(); // janela fechada durante a gravação

            var video = VideoEncodingProperties.CreateUncompressed(MediaEncodingSubtypes.Bgra8, (uint)size.Width, (uint)size.Height);
            _videoStream = new VideoStreamDescriptor(video);
            if (system is not null || microphone is not null)
            {
                var audio = AudioEncodingProperties.CreatePcm(AudioCapture.SampleRate, AudioCapture.Channels, 32);
                audio.Subtype = MediaEncodingSubtypes.Float;
                _audioStream = new AudioStreamDescriptor(audio);
                _source = new MediaStreamSource(_videoStream, _audioStream);
            }
            else
            {
                _source = new MediaStreamSource(_videoStream);
            }

            _source.BufferTime = TimeSpan.Zero;
            _source.CanSeek = false;
            _source.IsLive = true;
            _source.SampleRequested += OnSampleRequested;
            _source.Starting += (_, args) => args.Request.SetActualStartPosition(TimeSpan.Zero);
        }

        public RecordingOptions Options { get; }

        public void Start()
        {
            _capture.StartCapture();
            _system?.Start();
            _microphone?.Start();
        }

        public void RequestStop() => _stopRequested = true;

        public void ClearAudio()
        {
            _system?.Clear();
            _microphone?.Clear();
            _audioChunks = (long)(_owner._clock.Elapsed.TotalSeconds * 50);
        }

        public async Task TranscodeAsync()
        {
            var profile = MediaEncodingProfile.CreateMp4(VideoEncodingQuality.Auto);
            profile.Video.Width = (uint)_size.Width;
            profile.Video.Height = (uint)_size.Height;
            profile.Video.Bitrate = Options.VideoBitrate;
            profile.Video.FrameRate.Numerator = (uint)Options.FramesPerSecond;
            profile.Video.FrameRate.Denominator = 1;
            profile.Video.PixelAspectRatio.Numerator = 1;
            profile.Video.PixelAspectRatio.Denominator = 1;
            profile.Audio = _audioStream is null ? null : AudioEncodingProperties.CreateAac(AudioCapture.SampleRate, AudioCapture.Channels, 192_000);

            Directory.CreateDirectory(Path.GetDirectoryName(Options.OutputPath)!);
            var folder = await StorageFolder.GetFolderFromPathAsync(Path.GetDirectoryName(Options.OutputPath)!);
            var file = await folder.CreateFileAsync(Path.GetFileName(Options.OutputPath), CreationCollisionOption.GenerateUniqueName);
            using var output = await file.OpenAsync(FileAccessMode.ReadWrite);

            var transcoder = new MediaTranscoder { HardwareAccelerationEnabled = true };
            var prepared = await transcoder.PrepareMediaStreamSourceTranscodeAsync(_source, output, profile);
            if (!prepared.CanTranscode)
            {
                throw new InvalidOperationException($"O Windows não conseguiu preparar a codificação ({prepared.FailureReason}).");
            }

            await prepared.TranscodeAsync();
        }

        public void Dispose()
        {
            _pool.FrameArrived -= OnFrameArrived;
            _capture.Dispose();
            _pool.Dispose();
            _system?.Dispose();
            _microphone?.Dispose();
            lock (_frameLock)
            {
                foreach (var frame in _inUse.Keys.Append(_latest).OfType<Direct3D11CaptureFrame>().Distinct())
                {
                    frame.Dispose();
                }

                _inUse.Clear();
                _latest = null;
            }

        }

        private void OnFrameArrived(Direct3D11CaptureFramePool sender, object args)
        {
            var frame = sender.TryGetNextFrame();
            if (frame is null)
            {
                return;
            }

            // Janela redimensionada: o tamanho do vídeo continua o inicial (o conteúdo é cortado ou ganha bordas).
            if (frame.ContentSize.Width != _size.Width || frame.ContentSize.Height != _size.Height)
            {
                sender.Recreate(_device, DirectXPixelFormat.B8G8R8A8UIntNormalized, 3, _size);
            }

            lock (_frameLock)
            {
                var previous = _latest;
                _latest = frame;
                _latestVersion++;
                if (previous is not null && !_inUse.ContainsKey(previous))
                {
                    previous.Dispose();
                }
            }

            _owner._frameArrived.Release();
        }

        private void OnSampleRequested(MediaStreamSource sender, MediaStreamSourceSampleRequestedEventArgs args)
        {
            var request = args.Request;
            var deferral = request.GetDeferral();
            _ = Task.Run(() =>
            {
                try
                {
                    request.Sample = ReferenceEquals(request.StreamDescriptor, _audioStream) || request.StreamDescriptor is AudioStreamDescriptor ? NextAudioSample() : NextVideoSample();
                }
                catch (Exception ex)
                {
                    _owner.LogSampleFailure(ex);
                    request.Sample = null;
                }
                finally
                {
                    deferral.Complete();
                }
            });
        }

        /// <summary>Próximo quadro, no máximo na taxa configurada; repete o último se a tela não mudou.</summary>
        private MediaStreamSample? NextVideoSample()
        {
            while (true)
            {
                _owner._running.Wait();
                if (_stopRequested)
                {
                    return null;
                }

                var now = _owner._clock.Elapsed;
                var due = _lastVideoTime == TimeSpan.MinValue ? TimeSpan.Zero : _lastVideoTime + _frameInterval;
                if (now < due)
                {
                    Thread.Sleep(due - now);
                    continue;
                }

                Direct3D11CaptureFrame? frame;
                lock (_frameLock)
                {
                    frame = _latest;
                    var fresh = _latestVersion != _sentVersion;
                    var waitedTooLong = now - due > _frameInterval * 2;
                    if (frame is null || (!fresh && !waitedTooLong))
                    {
                        frame = null;
                    }
                    else
                    {
                        _sentVersion = _latestVersion;
                        _inUse[frame] = _inUse.GetValueOrDefault(frame) + 1;
                    }
                }

                if (frame is null)
                {
                    _owner._frameArrived.Wait(_frameInterval);
                    continue;
                }

                var timestamp = now;
                _lastVideoTime = timestamp;
                var sample = MediaStreamSample.CreateFromDirect3D11Surface(frame.Surface, timestamp);
                sample.Duration = _frameInterval;
                sample.Processed += (_, _) => Release(frame);
                return sample;
            }
        }

        private void Release(Direct3D11CaptureFrame frame)
        {
            lock (_frameLock)
            {
                var count = _inUse.GetValueOrDefault(frame) - 1;
                if (count > 0)
                {
                    _inUse[frame] = count;
                    return;
                }

                _inUse.Remove(frame);
                if (!ReferenceEquals(frame, _latest))
                {
                    frame.Dispose();
                }
            }
        }

        /// <summary>Bloco de 20 ms no ritmo do relógio: mistura sistema e microfone, silêncio onde faltar.</summary>
        private MediaStreamSample? NextAudioSample()
        {
            var timestamp = TimeSpan.FromTicks(_audioChunks * TimeSpan.TicksPerSecond / 50);
            while (true)
            {
                _owner._running.Wait();
                if (_stopRequested)
                {
                    return null;
                }

                var wait = timestamp + TimeSpan.FromMilliseconds(20) - _owner._clock.Elapsed;
                if (wait <= TimeSpan.Zero)
                {
                    break;
                }

                Thread.Sleep(wait);
            }

            var buffer = new float[AudioChunkFrames * AudioCapture.Channels];
            if (_system is not null)
            {
                _system.Read(buffer, AudioChunkFrames, mix: false);
            }

            _microphone?.Read(buffer, AudioChunkFrames, mix: _system is not null);

            var bytes = new byte[buffer.Length * sizeof(float)];
            System.Buffer.BlockCopy(buffer, 0, bytes, 0, bytes.Length);
            var sample = MediaStreamSample.CreateFromBuffer(bytes.AsBuffer(), timestamp);
            sample.Duration = TimeSpan.FromMilliseconds(20);
            _audioChunks++;
            return sample;
        }
    }
}
