using System.Collections.Concurrent;

namespace Dynasland.Infrastructure.Windows;

/// <summary>
/// Thread STA dedicada para APIs do Windows que exigem apartamento single-threaded em apps desktop
/// (ex.: área de transferência WinRT, que falha com RPC_E_WRONG_THREAD em threads MTA). As esperas
/// síncronas numa thread STA do .NET bombeiam as mensagens COM, então operações assíncronas do Windows
/// podem ser aguardadas com <c>GetAwaiter().GetResult()</c> dentro do trabalho.
/// </summary>
public sealed class StaWorker : IDisposable
{
    private readonly BlockingCollection<Action> _queue = new();
    private readonly Thread _thread;

    public StaWorker(string name)
    {
        _thread = new Thread(Loop) { IsBackground = true, Name = name };
        _thread.SetApartmentState(ApartmentState.STA);
        _thread.Start();
    }

    public Task<T> Run<T>(Func<T> work)
    {
        var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            _queue.Add(() =>
            {
                try
                {
                    completion.SetResult(work());
                }
                catch (Exception ex)
                {
                    completion.SetException(ex);
                }
            });
        }
        catch (InvalidOperationException)
        {
            completion.SetException(new ObjectDisposedException(nameof(StaWorker)));
        }

        return completion.Task;
    }

    public void Dispose() => _queue.CompleteAdding();

    private void Loop()
    {
        foreach (var work in _queue.GetConsumingEnumerable())
        {
            work();
        }
    }
}
