using Dynasland.Core.Activities;
using Dynasland.Core.Events;
using Dynasland.Core.Interfaces;
using Dynasland.Core.Modules;

namespace Dynasland.Modules.Bluetooth;

/// <summary>
/// Avisa quando um dispositivo Bluetooth pareado (fone, controle, caixa de som) conecta ou desconecta,
/// com a bateria quando o Windows informa, e alerta quando ela está acabando.
/// </summary>
public sealed class BluetoothModule(IBluetoothService bluetooth, IEventBus bus, TimeProvider time, IModuleOptions options) : IIslandModule
{
    public const string ModuleId = "bluetooth";
    public const string ActivityPrefix = "bluetooth.device.";
    public const string BatteryActivityPrefix = "bluetooth.battery.";

    private static readonly TimeSpan DisplayDuration = TimeSpan.FromSeconds(4);

    // O mesmo aparelho pode aparecer como dois dispositivos (clássico e de baixa energia).
    private static readonly TimeSpan DuplicateWindow = TimeSpan.FromSeconds(5);

    // A bateria só é publicada pelo Windows um pouco depois de a conexão se estabelecer.
    private static readonly TimeSpan BatteryReadDelay = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan BatteryCheckInterval = TimeSpan.FromMinutes(5);

    private readonly Lock _lock = new();
    private readonly Dictionary<string, (bool Connected, DateTimeOffset At)> _announced = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _lowBatteryWarned = new(StringComparer.OrdinalIgnoreCase);
    private ITimer? _batteryTimer;

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
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        bluetooth.ConnectionChanged -= OnConnectionChanged;
        lock (_lock)
        {
            _batteryTimer?.Dispose();
            _batteryTimer = null;
            _announced.Clear();
            _lowBatteryWarned.Clear();
        }

        await bluetooth.StopAsync();
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
