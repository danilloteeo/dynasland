using System.Windows;
using System.Windows.Interop;
using Dynasland.Core.Interfaces;
using Dynasland.Core.Layout;
using Dynasland.Infrastructure.Windows;
using Microsoft.Extensions.Logging;

namespace Dynasland.App.Services;

/// <summary>Posiciona a janela da ilha no topo do monitor configurado (centro, esquerda ou direita).</summary>
public sealed class IslandPlacementService(
    IMonitorService monitorService,
    IIslandSettingsService settings,
    ILogger<IslandPlacementService> logger)
{
    /// <summary>Monitor (nome do dispositivo) onde a ilha foi posicionada por último.</summary>
    public string? CurrentMonitor { get; private set; }

    public void Place(Window window)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero)
        {
            return;
        }

        var monitor = MonitorSelector.Select(monitorService.GetMonitors(), settings.Current.MonitorDeviceName);
        if (monitor is null)
        {
            logger.LogWarning("Nenhum monitor encontrado; posicionamento ignorado.");
            return;
        }

        CurrentMonitor = monitor.DeviceName;
        var current = settings.Current;
        var topMargin = IslandPlacementCalculator.Top(current.Shape, current.TopMargin) * monitor.Scale;

        // Primeiro movimento usa a largura estimada no DPI do monitor de destino.
        // Se o monitor tiver outro DPI, o WPF redimensiona a janela (WM_DPICHANGED)
        // e a posição é recalculada com a largura real.
        var estimatedWidth = (int)Math.Round(window.Width * monitor.Scale);
        WindowInterop.MoveTopmost(hwnd, IslandPlacementCalculator.Place(monitor.WorkArea, estimatedWidth, current.Position, topMargin));

        var actualWidth = WindowInterop.GetBounds(hwnd).Width;
        if (actualWidth > 0 && actualWidth != estimatedWidth)
        {
            WindowInterop.MoveTopmost(hwnd, IslandPlacementCalculator.Place(monitor.WorkArea, actualWidth, current.Position, topMargin));
        }
    }
}
