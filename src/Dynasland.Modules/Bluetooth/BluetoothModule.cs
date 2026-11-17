using Dynasland.Core.Activities;
using Dynasland.Core.Events;
using Dynasland.Core.Interfaces;
using Dynasland.Core.Models;
using Dynasland.Core.Modules;
using Dynasland.Modules.Common;

namespace Dynasland.Modules.Bluetooth;

/// <summary>
/// Avisa quando um dispositivo Bluetooth pareado (fone, controle, caixa de som) conecta ou desconecta,
/// com a bateria quando o Windows informa, e alerta quando ela está acabando.
/// </summary>
public sealed class BluetoothModule(
    IBluetoothService bluetooth,
    IEventBus bus,
    TimeProvider time,
    IModuleOptions options,
    IShortcutRegistry? shortcuts = null,
    IUiDispatcher? ui = null) : IIslandModule
{
    public const string ModuleId = "bluetooth";
    public const string ActivityPrefix = "bluetooth.device.";
    public const string BatteryActivityPrefix = "bluetooth.battery.";
    public const string PanelActivityId = "bluetooth.panel";

    private static readonly TimeSpan DisplayDuration = TimeSpan.FromSeconds(4);

    // O mesmo aparelho pode aparecer como dois dispositivos (clássico e de baixa energia).
    private static readonly TimeSpan DuplicateWindow = TimeSpan.FromSeconds(5);

    // A bateria só é publicada pelo Windows um pouco depois de a conexão se estabelecer.
    private static readonly TimeSpan BatteryReadDelay = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan BatteryCheckInterval = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan PanelRefreshInterval = TimeSpan.FromSeconds(15);

    private readonly Lock _lock = new();
    private readonly Dictionary<string, (bool Connected, DateTimeOffset At)> _announced = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _lowBatteryWarned = new(StringComparer.OrdinalIgnoreCase);
    private ITimer? _batteryTimer;
    private ITimer? _panelTimer;
    private BluetoothDevicesContent? _panel;
    private IDisposable? _shortcut;

    public static readonly ModuleOption AnnounceConnections = ModuleOption.Toggle(
        "announceConnections", "Avisar ao conectar e desconectar", "Mostra na ilha quando um dispositivo pareado conecta ou desconecta.");

    public static readonly ModuleOption LowBatteryAlert = ModuleOption.Toggle(
        "lowBatteryAlert", "Avisar bateria baixa", "Alerta quando a bateria de um dispositivo conectado fica abaixo do limite (nem todo dispositivo informa a bateria).");

    public static readonly ModuleOption LowBatteryThreshold = ModuleOption.Slider(
        "lowBatteryThreshold", "Bateria baixa abaixo de", "Nível de bateria que dispara o aviso.", 20, 5, 50, 5, "%");

    public string Id => ModuleId;

    public string DisplayName => "Bluetooth";

    public string Description => "Avisos de conexão, desconexão e bateria baixa de fones, controles e outros dispositivos Bluetooth.";

    public IReadOnlyList<ModuleOption> Options => [AnnounceConnections, LowBatteryAlert, LowBatteryThreshold];

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        bluetooth.ConnectionChanged += OnConnectionChanged;
        if (!await bluetooth.StartAsync(cancellationToken))
        {
            bluetooth.ConnectionChanged -= OnConnectionChanged;
            throw new ModuleUnavailableException("Este computador não tem um adaptador Bluetooth ligado.");
        }

        lock (_lock)
        {
            _batteryTimer = time.CreateTimer(_ => _ = CheckBatteriesAsync(), null, BatteryCheckInterval, BatteryCheckInterval);
            _panel = new BluetoothDevicesContent(OnPresentationChanged);
        }

        _shortcut = shortcuts?.Register(new IslandShortcut(PanelActivityId, ModuleId, Glyphs.Bluetooth, "Dispositivos", OpenPanel));
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        bluetooth.ConnectionChanged -= OnConnectionChanged;
        _shortcut?.Dispose();
        _shortcut = null;
        lock (_lock)
        {
            _batteryTimer?.Dispose();
            _batteryTimer = null;
            _panelTimer?.Dispose();
            _panelTimer = null;
            _panel = null;
            _announced.Clear();
            _lowBatteryWarned.Clear();
        }

        await bluetooth.StopAsync();
    }

    public void OpenPanel()
    {
        if (_panel is { } panel)
        {
            bus.PostActivity(new IslandActivity(PanelActivityId, ModuleId, ActivityKind.Transient, panel) { OpenInteractive = true });
        }
    }

    /// <summary>A bateria só é consultada enquanto o painel está visível.</summary>
    private void OnPresentationChanged(IslandState? state)
    {
        lock (_lock)
        {
            var visible = state is IslandState.Interactive or IslandState.Expanded;
            if (visible && _panelTimer is null && _panel is not null)
            {
                _panelTimer = time.CreateTimer(_ => _ = RefreshPanelAsync(), null, TimeSpan.Zero, PanelRefreshInterval);
            }
            else if (!visible && _panelTimer is not null)
            {
                _panelTimer.Dispose();
                _panelTimer = null;
            }
        }
    }

    private async Task RefreshPanelAsync()
    {
        var panel = _panel;
        if (panel is null)
        {
            return;
        }

        var threshold = (int)options.Get(ModuleId, LowBatteryThreshold);
        var rows = new List<BluetoothDeviceRow>();
        foreach (var device in bluetooth.Devices.Where(d => d.IsConnected).OrderBy(d => d.Name, StringComparer.CurrentCultureIgnoreCase))
        {
            // O mesmo aparelho pode aparecer duas vezes (clássico e BLE): fica a linha que tem bateria.
            int? battery = null;
            try
            {
                battery = await bluetooth.GetBatteryAsync(device.Id);
            }
            catch (Exception)
            {
                // Sem leitura a linha aparece do mesmo jeito.
            }

            var existing = rows.FindIndex(r => string.Equals(r.Name, device.Name, StringComparison.OrdinalIgnoreCase));
            if (existing >= 0)
            {
                if (battery is null || rows[existing].HasBattery)
                {
                    continue;
                }

                rows.RemoveAt(existing);
            }

            rows.Add(new BluetoothDeviceRow(device.Name, device.Kind, battery, threshold));
        }

        if (ui is null)
        {
            panel.Apply(rows);
        }
        else
        {
            ui.Post(() => panel.Apply(rows));
        }
    }

    public static string ConnectionMessage(bool connected, int? battery) =>
        connected ? battery is { } percent ? $"Conectado · bateria {percent}%" : "Conectado" : "Desconectado";

    private void OnConnectionChanged(object? sender, BluetoothDeviceInfo device) => _ = AnnounceAsync(device);

    private async Task AnnounceAsync(BluetoothDeviceInfo device)
    {
        var now = time.GetUtcNow();
        lock (_lock)
        {
            if (!device.IsConnected)
            {
                // Ao reconectar, a bateria baixa volta a ser avisada.
                _lowBatteryWarned.Remove(device.Name);
            }

            if (_announced.TryGetValue(device.Name, out var last) && last.Connected == device.IsConnected && now - last.At < DuplicateWindow)
            {
                return;
            }

            _announced[device.Name] = (device.IsConnected, now);
        }

        if (!options.IsOn(ModuleId, AnnounceConnections))
        {
            return;
        }

        int? battery = null;
        if (device.IsConnected)
        {
            try
            {
                await Task.Delay(BatteryReadDelay, time);
                battery = await bluetooth.GetBatteryAsync(device.Id);
            }
            catch (Exception)
            {
                // Sem bateria o aviso de conexão sai do mesmo jeito.
            }
        }

        var content = new StatusContent(StatusKind.Info, device.Name, ConnectionMessage(device.IsConnected, battery));
        bus.PostActivity(new IslandActivity(ActivityPrefix + device.Name, ModuleId, ActivityKind.Transient, content)
        {
            Duration = DisplayDuration,
            GroupKey = ModuleId,
        });
    }

    private async Task CheckBatteriesAsync()
    {
        if (!options.IsOn(ModuleId, LowBatteryAlert))
        {
            return;
        }

        var threshold = options.Get(ModuleId, LowBatteryThreshold);
        foreach (var device in bluetooth.Devices.Where(d => d.IsConnected))
        {
            int? battery;
            try
            {
                battery = await bluetooth.GetBatteryAsync(device.Id);
            }
            catch (Exception)
            {
                continue;
            }

            if (battery is not { } percent || percent > threshold)
            {
                continue;
            }

            lock (_lock)
            {
                if (!_lowBatteryWarned.Add(device.Name))
                {
                    continue;
                }
            }

            var content = new StatusContent(StatusKind.Error, device.Name, $"Bateria em {percent}%");
            bus.PostActivity(new IslandActivity(BatteryActivityPrefix + device.Name, ModuleId, ActivityKind.Transient, content)
            {
                Priority = ActivityPriority.High,
                Duration = DisplayDuration,
            });
        }
    }
}
