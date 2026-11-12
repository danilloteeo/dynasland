using System.Windows;
using System.Windows.Interop;
using Dynasland.Core.Interfaces;
using Dynasland.Core.Layout;
using Dynasland.Infrastructure.Windows;
using Microsoft.Extensions.Logging;

namespace Dynasland.App.Services;

/// <summary>Posiciona a janela da ilha na borda configurada do monitor (embaixo ou em cima; centro, esquerda ou direita).</summary>
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
        // A margem vale a partir da borda escolhida; a encaixada cola na borda (margem 0).
        var margin = IslandPlacementCalculator.Top(current.Shape, current.TopMargin) * monitor.Scale;

        // Primeiro movimento usa o tamanho estimado no DPI do monitor de destino; se o DPI for outro,
        // o WPF redimensiona a janela (WM_DPICHANGED) e a posição é recalculada com o tamanho real.
        var estimatedWidth = (int)Math.Round(window.Width * monitor.Scale);
        var estimatedHeight = (int)Math.Round(window.Height * monitor.Scale);
        WindowInterop.MoveTopmost(hwnd, IslandPlacementCalculator.PlaceOnEdge(monitor.WorkArea, estimatedWidth, estimatedHeight, current.Position, current.Edge, margin));

        var actual = WindowInterop.GetBounds(hwnd);
        if (actual.Width > 0 && (actual.Width != estimatedWidth || actual.Height != estimatedHeight))
        {
            WindowInterop.MoveTopmost(hwnd, IslandPlacementCalculator.PlaceOnEdge(monitor.WorkArea, actual.Width, actual.Height, current.Position, current.Edge, margin));
        }
    }
}
