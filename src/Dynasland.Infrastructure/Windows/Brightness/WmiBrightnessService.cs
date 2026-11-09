using System.Management;
using Dynasland.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace Dynasland.Infrastructure.Windows.Brightness;

/// <summary>
/// Brilho de telas integradas (notebooks, tablets) via WMI: WmiMonitorBrightness para ler,
/// WmiMonitorBrightnessMethods para alterar e WmiMonitorBrightnessEvent para ser notificado.
/// Monitores externos não expõem essas classes; nesse caso o serviço informa falta de suporte.
/// </summary>
public sealed class WmiBrightnessService(ILogger<WmiBrightnessService> logger) : IBrightnessService
{
    private const string Scope = @"root\WMI";

    private ManagementEventWatcher? _watcher;

    public int? Current { get; private set; }

    public event EventHandler<int>? Changed;

    public Task<bool> StartAsync(CancellationToken cancellationToken = default) => Task.Run(() =>
    {
        Current = QueryCurrent();
        if (Current is null)
        {
            return false;
        }

        _watcher = new ManagementEventWatcher(new ManagementScope(Scope), new EventQuery("SELECT * FROM WmiMonitorBrightnessEvent"));
        _watcher.EventArrived += OnBrightnessEvent;
        _watcher.Start();
        return true;
    }, cancellationToken);

    public Task StopAsync()
    {
        if (_watcher is not null)
        {
            _watcher.EventArrived -= OnBrightnessEvent;
            _watcher.Stop();
            _watcher.Dispose();
            _watcher = null;
        }

        return Task.CompletedTask;
    }

    public Task SetAsync(int percent) => Task.Run(() =>
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(Scope, "SELECT * FROM WmiMonitorBrightnessMethods WHERE Active = TRUE");
            foreach (var instance in searcher.Get().Cast<ManagementObject>())
            {
                using (instance)
                {
                    instance.InvokeMethod("WmiSetBrightness", [(uint)1, (byte)Math.Clamp(percent, 0, 100)]);
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Falha ao alterar o brilho.");
        }
    });

    private void OnBrightnessEvent(object sender, EventArrivedEventArgs e)
    {
        try
        {
            var brightness = Convert.ToInt32(e.NewEvent["Brightness"]);
            Current = brightness;
            Changed?.Invoke(this, brightness);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Evento de brilho inválido.");
        }
    }

    private int? QueryCurrent()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(Scope, "SELECT CurrentBrightness FROM WmiMonitorBrightness WHERE Active = TRUE");
            foreach (var instance in searcher.Get().Cast<ManagementObject>())
            {
                using (instance)
                {
                    return Convert.ToInt32(instance["CurrentBrightness"]);
                }
            }
        }
        catch (Exception ex)
        {
            // "Não suportado" é o resultado esperado em desktops com monitores externos.
            logger.LogDebug(ex, "Brilho via WMI indisponível.");
        }

        return null;
    }
}
