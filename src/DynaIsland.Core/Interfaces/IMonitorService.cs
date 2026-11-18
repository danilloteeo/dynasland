using DynaIsland.Core.Models;

namespace DynaIsland.Core.Interfaces;

public interface IMonitorService
{
    /// <summary>Lista os monitores conectados neste momento, da esquerda para a direita.</summary>
    IReadOnlyList<DisplayMonitor> GetMonitors();
}
