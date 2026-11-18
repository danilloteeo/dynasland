using System.Windows.Threading;
using DynaIsland.Core.Interfaces;

namespace DynaIsland.App.Services;

public sealed class WpfUiDispatcher(Dispatcher dispatcher) : IUiDispatcher
{
    public void Post(Action action) => dispatcher.BeginInvoke(DispatcherPriority.Normal, action);
}
