namespace Dynasland.Core.Interfaces;

/// <summary>Agenda trabalho na thread de interface sem que o chamador dependa de WPF.</summary>
public interface IUiDispatcher
{
    void Post(Action action);
}
