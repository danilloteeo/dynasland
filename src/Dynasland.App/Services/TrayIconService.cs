using System.Drawing;
using System.Windows.Forms;
using Dynasland.Core.Interfaces;
using Dynasland.Core.Models;
using Microsoft.Extensions.Logging;
using Application = System.Windows.Application;

namespace Dynasland.App.Services;

/// <summary>
/// Ícone na bandeja do sistema e seu menu de contexto. Todas as ações alteram as
/// configurações persistidas; a janela da ilha reage a <see cref="IIslandSettingsService.Changed"/>.
/// </summary>
public sealed class TrayIconService(
    IIslandSettingsService settings,
    IMonitorService monitorService,
    ILogger<TrayIconService> logger) : IDisposable
{
    private NotifyIcon? _notifyIcon;
    private Icon? _icon;

    public event EventHandler? ExitRequested;

    public void Initialize()
    {
        var menu = new ContextMenuStrip
        {
            Renderer = new DarkMenuRenderer(),
            ShowImageMargin = false,
            ShowCheckMargin = true,
            Font = new Font("Segoe UI", 9f),
        };
        menu.Opening += (_, _) => BuildMenu(menu);
        BuildMenu(menu);

        _icon = LoadIcon();
        _notifyIcon = new NotifyIcon
        {
            Icon = _icon,
            Text = "Dynasland",
            ContextMenuStrip = menu,
            Visible = true,
        };
        _notifyIcon.MouseClick += (_, e) =>
        {
            if (e.Button == MouseButtons.Left)
            {
                ToggleVisibility();
            }
        };
    }

    private void BuildMenu(ContextMenuStrip menu)
    {
        var current = settings.Current;
        menu.Items.Clear();

        menu.Items.Add(new ToolStripMenuItem(current.IsVisible ? "Ocultar ilha" : "Mostrar ilha", null, (_, _) => ToggleVisibility()));
        menu.Items.Add(BuildMonitorMenu(current));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(new ToolStripMenuItem("Sair do Dynasland", null, (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty)));
    }

    private ToolStripMenuItem BuildMonitorMenu(IslandSettings current)
    {
        var root = new ToolStripMenuItem("Exibir no monitor");
        var monitors = monitorService.GetMonitors();
        var preferredConnected = monitors.Any(m =>
            string.Equals(m.DeviceName, current.MonitorDeviceName, StringComparison.OrdinalIgnoreCase));

        root.DropDownItems.Add(new ToolStripMenuItem("Monitor principal", null, (_, _) => SelectMonitor(null))
        {
            Checked = current.MonitorDeviceName is null || !preferredConnected,
        });
        root.DropDownItems.Add(new ToolStripSeparator());

        foreach (var monitor in monitors)
        {
            var deviceName = monitor.DeviceName;
            root.DropDownItems.Add(new ToolStripMenuItem(Describe(monitor), null, (_, _) => SelectMonitor(deviceName))
            {
                Checked = string.Equals(deviceName, current.MonitorDeviceName, StringComparison.OrdinalIgnoreCase),
            });
        }

        root.DropDown.Renderer = new DarkMenuRenderer();
        return root;
    }

    private static string Describe(DisplayMonitor monitor)
    {
        var name = monitor.DisplayNumber is { } number ? $"Monitor {number}" : monitor.DeviceName;
        var scale = (int)Math.Round(monitor.Scale * 100);
        var primary = monitor.IsPrimary ? " · principal" : string.Empty;
        return $"{name} ({monitor.Bounds.Width}×{monitor.Bounds.Height}, {scale}%){primary}";
    }

    private void ToggleVisibility() => Update(s => s with { IsVisible = !s.IsVisible });

    private void SelectMonitor(string? deviceName) => Update(s => s with { MonitorDeviceName = deviceName });

    private async void Update(Func<IslandSettings, IslandSettings> change)
    {
        try
        {
            await settings.UpdateAsync(change);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Falha ao salvar configuração a partir da bandeja.");
        }
    }

    private Icon LoadIcon()
    {
        try
        {
            var resource = Application.GetResourceStream(new Uri("pack://application:,,,/Assets/dynasland.ico"));
            if (resource is not null)
            {
                using var stream = resource.Stream;
                return new Icon(stream, SystemInformation.SmallIconSize);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Ícone da bandeja não carregado; usando ícone padrão.");
        }

        return (Icon)SystemIcons.Application.Clone();
    }

    public void Dispose()
    {
        if (_notifyIcon is not null)
        {
            _notifyIcon.Visible = false;
            _notifyIcon.ContextMenuStrip?.Dispose();
            _notifyIcon.Dispose();
            _notifyIcon = null;
        }

        _icon?.Dispose();
    }
}
