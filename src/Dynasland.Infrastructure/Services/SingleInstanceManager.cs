namespace Dynasland.Infrastructure.Services;

/// <summary>
/// Garante uma única instância por sessão de usuário usando um Mutex nomeado.
/// Uma segunda instância pode sinalizar a primeira por um EventWaitHandle nomeado.
/// </summary>
public sealed class SingleInstanceManager(string applicationId) : IDisposable
{
    private readonly string _mutexName = $@"Local\{applicationId}.SingleInstance";
    private readonly string _activationEventName = $@"Local\{applicationId}.Activate";

    private Mutex? _mutex;
    private bool _ownsMutex;
    private EventWaitHandle? _activationEvent;
    private RegisteredWaitHandle? _activationWait;

    /// <summary>Disparado (em thread do pool) quando outra instância pede ativação.</summary>
    public event EventHandler? ActivationRequested;

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
            (_, _) => ActivationRequested?.Invoke(this, EventArgs.Empty),
            state: null,
            Timeout.Infinite,
            executeOnlyOnce: false);
        return true;
    }

    /// <summary>Pede à instância já em execução que se mostre.</summary>
    public bool SignalFirstInstance()
    {
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
        _activationWait?.Unregister(null);
        _activationEvent?.Dispose();
        if (_ownsMutex)
        {
            _mutex?.ReleaseMutex();
        }

        _mutex?.Dispose();
    }
}
