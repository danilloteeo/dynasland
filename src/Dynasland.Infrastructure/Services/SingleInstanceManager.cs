using System.Diagnostics;
using System.IO.Pipes;
using System.Text;

namespace Dynasland.Infrastructure.Services;

/// <summary>
/// Garante uma única instância por sessão de usuário usando um Mutex nomeado.
/// Uma segunda instância sinaliza a primeira por um EventWaitHandle nomeado ou, se tiver argumentos
/// (ex.: <c>--central</c>), os repassa por um pipe nomeado restrito ao usuário atual.
/// </summary>
public sealed class SingleInstanceManager(string applicationId) : IDisposable
{
    private readonly string _mutexName = $@"Local\{applicationId}.SingleInstance";
    private readonly string _activationEventName = $@"Local\{applicationId}.Activate";

    // Pipes são globais à máquina: o nome inclui a sessão para não cruzar usuários.
    private readonly string _pipeName = $"{applicationId}.Args.{Process.GetCurrentProcess().SessionId}";

    private readonly CancellationTokenSource _stop = new();
    private Mutex? _mutex;
    private bool _ownsMutex;
    private EventWaitHandle? _activationEvent;
    private RegisteredWaitHandle? _activationWait;

    /// <summary>
    /// Disparado (em thread do pool) quando outra instância pede ativação, com os argumentos dela
    /// (vazio = apenas mostrar).
    /// </summary>
    public event EventHandler<IReadOnlyList<string>>? ActivationRequested;

    /// <returns><c>true</c> se esta é a primeira instância.</returns>
    public bool TryAcquire()
    {
        _mutex = new Mutex(initiallyOwned: true, _mutexName, out var createdNew);
        _ownsMutex = createdNew;
        if (!createdNew)
        {
            return false;
        }

        _activationEvent = new EventWaitHandle(false, EventResetMode.AutoReset, _activationEventName);
        _activationWait = ThreadPool.RegisterWaitForSingleObject(
            _activationEvent,
            (_, _) => ActivationRequested?.Invoke(this, []),
            state: null,
            Timeout.Infinite,
            executeOnlyOnce: false);
        _ = ListenForArgumentsAsync(_stop.Token);
        return true;
    }

    /// <summary>Pede à instância já em execução que se mostre (e trate os argumentos, se houver).</summary>
    public bool SignalFirstInstance(IReadOnlyList<string>? args = null)
    {
        if (args is { Count: > 0 } && TrySendArguments(args))
        {
            return true;
        }

        if (!EventWaitHandle.TryOpenExisting(_activationEventName, out var handle))
        {
            return false;
        }

        using (handle)
        {
            return handle.Set();
        }
    }

    public void Dispose()
    {
        _stop.Cancel();
        _activationWait?.Unregister(null);
        _activationEvent?.Dispose();
        if (_ownsMutex)
        {
            _mutex?.ReleaseMutex();
        }

        _mutex?.Dispose();
    }

    private bool TrySendArguments(IReadOnlyList<string> args)
    {
        try
        {
            using var client = new NamedPipeClientStream(".", _pipeName, PipeDirection.Out, PipeOptions.CurrentUserOnly);
            client.Connect(1000);
            var bytes = Encoding.UTF8.GetBytes(string.Join('\n', args));
            client.Write(bytes);
            client.Flush();
            return true;
        }
        catch (Exception ex) when (ex is TimeoutException or IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private async Task ListenForArgumentsAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await using var server = new NamedPipeServerStream(
                    _pipeName, PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                await server.WaitForConnectionAsync(cancellationToken);

                using var reader = new StreamReader(server, Encoding.UTF8);
                var text = await reader.ReadToEndAsync(cancellationToken);
                var args = text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                ActivationRequested?.Invoke(this, args);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (IOException)
            {
                // Cliente desconectou no meio; espera o próximo.
            }
        }
    }
}
