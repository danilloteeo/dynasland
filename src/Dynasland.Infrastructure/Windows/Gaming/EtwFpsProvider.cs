using System.Collections.Concurrent;
using System.Diagnostics;
using Dynasland.Core.Interfaces;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Session;
using Microsoft.Extensions.Logging;

namespace Dynasland.Infrastructure.Windows.Gaming;

/// <summary>
/// FPS pelos eventos de apresentação do DirectX publicados no ETW (o mesmo princípio do PresentMon):
/// cada "Present" de um processo é um quadro. Não toca no jogo. Uma sessão ETW em tempo real exige
/// administrador ou o grupo "Usuários de log de desempenho"; sem isso, fica indisponível com o motivo.
/// </summary>
public sealed class EtwFpsProvider(ILogger<EtwFpsProvider> logger) : IFpsProvider, IDisposable
{
    private const string SessionName = "Dynasland-FPS";

    // Microsoft-Windows-DXGI (Present_Start = 42) e Microsoft-Windows-D3D9 (Present_Start = 1).
    private static readonly Guid DxgiProvider = new("CA11C036-0102-4A2D-A6AD-F03CFED5D3C9");
    private static readonly Guid D3D9Provider = new("783ACA0A-790E-4D7F-8451-AA850511C6B9");

    private readonly ConcurrentDictionary<int, ConcurrentQueue<long>> _presents = new();
    private TraceEventSession? _session;
    private Thread? _thread;

    public string? UnavailableReason { get; private set; }

    public bool Start()
    {
        if (_session is not null)
        {
            return true;
        }

        try
        {
            var session = new TraceEventSession(SessionName) { StopOnDispose = true };
            session.EnableProvider(DxgiProvider);
            session.EnableProvider(D3D9Provider);
            session.Source.Dynamic.All += OnEvent;
            _session = session;
            _thread = new Thread(() =>
            {
                try
                {
                    session.Source.Process();
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Sessão de FPS encerrada com erro.");
                }
            })
            { IsBackground = true, Name = "Dynasland.Fps" };
            _thread.Start();
            UnavailableReason = null;
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            UnavailableReason = "Para medir FPS, execute o Dynasland como administrador ou entre no grupo \"Usuários de log de desempenho\" do Windows.";
        }
        catch (Exception ex)
        {
            logger.LogInformation(ex, "Medição de FPS indisponível.");
            UnavailableReason = "O Windows não permitiu iniciar a medição de FPS.";
        }

        _session = null;
        return false;
    }

    public void Stop()
    {
        _session?.Dispose();
        _session = null;
        _presents.Clear();
    }

    public void Dispose() => Stop();

    public double? GetFps(int processId)
    {
        if (!_presents.TryGetValue(processId, out var queue))
        {
            return null;
        }

        var cutoff = Stopwatch.GetTimestamp() - Stopwatch.Frequency;
        while (queue.TryPeek(out var oldest) && oldest < cutoff)
        {
            queue.TryDequeue(out _);
        }

        return queue.IsEmpty ? null : queue.Count;
    }

    private void OnEvent(TraceEvent data)
    {
        var isPresent = (data.ProviderGuid == DxgiProvider && (int)data.ID == 42) || (data.ProviderGuid == D3D9Provider && (int)data.ID == 1);
        if (!isPresent)
        {
            return;
        }

        var queue = _presents.GetOrAdd(data.ProcessID, _ => new ConcurrentQueue<long>());
        queue.Enqueue(Stopwatch.GetTimestamp());

        // Limita a memória de processos que apresentam muito e nunca são consultados.
        while (queue.Count > 1000 && queue.TryDequeue(out _))
        {
        }
    }
}
