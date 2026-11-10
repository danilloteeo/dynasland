using System.Windows.Threading;
using Dynasland.App.Views;
using Dynasland.Core.Interfaces;
using Dynasland.Core.Models;

namespace Dynasland.App.Services;

/// <summary>Abre a sobreposição de seleção de área na thread de interface.</summary>
public sealed class RegionSelector(Dispatcher dispatcher) : IRegionSelector
{
    public async Task<PixelRect?> SelectAsync(CapturedImage background, PixelRect desktop)
    {
        var window = await dispatcher.InvokeAsync(() =>
        {
            var overlay = new RegionSelectorWindow(background.Png, background.Width, background.Height, desktop);
            overlay.Show();
            overlay.Activate();
            return overlay;
        });

        return await window.Result.Task;
    }
}
