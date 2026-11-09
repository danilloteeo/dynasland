using System.Windows.Threading;
using Dynasland.Core.Interfaces;

namespace Dynasland.App.Services;

public sealed class WpfUiDispatcher(Dispatcher dispatcher) : IUiDispatcher
{
    public void Post(Action action) => dispatcher.BeginInvoke(DispatcherPriority.Normal, action);
}
