using Dynasland.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Windows.Devices.Bluetooth;
using Windows.Devices.Enumeration;

namespace Dynasland.Infrastructure.Windows.Bluetooth;

/// <summary>
/// Dispositivos Bluetooth pareados (clássico e de baixa energia) acompanhados por um DeviceWatcher do
/// Windows: a propriedade <c>System.Devices.Aep.IsConnected</c> muda ao conectar e desconectar.
/// A bateria vem da propriedade que o Windows mantém para fones e similares; nem todo dispositivo informa.
/// </summary>
public sealed class WinRtBluetoothService(ILogger<WinRtBluetoothService> logger) : IBluetoothService
{
    private const string ClassicProtocol = "{e0cbf06c-cd8b-4647-bb8a-263b43f0f974}";
    private const string LowEnergyProtocol = "{bb7bb05e-5972-42b5-94fc-76eaa7084d49}";
    private const string IsConnectedProperty = "System.Devices.Aep.IsConnected";
    private const string AepContainerProperty = "System.Devices.Aep.ContainerId";
    private const string ContainerProperty = "System.Devices.ContainerId";

    /// <summary>DEVPKEY_Bluetooth_Battery: porcentagem informada pelo perfil de mãos-livres.</summary>
    private const string BatteryProperty = "{104EA319-6EE2-4701-BD47-8DDBF425BBE5} 2";

    private static readonly string PairedSelector =
        $"(System.Devices.Aep.ProtocolId:=\"{ClassicProtocol}\" OR System.Devices.Aep.ProtocolId:=\"{LowEnergyProtocol}\")"
        + " AND System.Devices.Aep.IsPaired:=System.StructuredQueryType.Boolean#True";

    private readonly Lock _lock = new();
    private readonly Dictionary<string, Entry> _devices = new(StringComparer.Ordinal);
    private DeviceWatcher? _watcher;
    private bool _enumerated;

    public event EventHandler<BluetoothDeviceInfo>? ConnectionChanged;

    public IReadOnlyList<BluetoothDeviceInfo> Devices
    {
        get
        {
            lock (_lock)
            {
                return [.. _devices.Select(d => new BluetoothDeviceInfo(d.Key, d.Value.Name, d.Value.IsConnected))];
            }
        }
    }

    public async Task<bool> StartAsync(CancellationToken cancellationToken = default)
    {
        // WinRT obtido fora da thread de UI (os eventos do watcher chegam em threads do pool).
        return await Task.Run(async () =>
        {
            if (await BluetoothAdapter.GetDefaultAsync() is null)
            {
                return false;
            }

            var watcher = DeviceInformation.CreateWatcher(PairedSelector, [IsConnectedProperty, AepContainerProperty], DeviceInformationKind.AssociationEndpoint);
            watcher.Added += OnAdded;
            watcher.Updated += OnUpdated;
            watcher.Removed += OnRemoved;
            watcher.EnumerationCompleted += OnEnumerationCompleted;
            lock (_lock)
            {
                _devices.Clear();
                _enumerated = false;
                _watcher = watcher;
            }

            watcher.Start();
            return true;
        }, cancellationToken);
    }

    public Task StopAsync()
    {
        DeviceWatcher? watcher;
        lock (_lock)
        {
            watcher = _watcher;
            _watcher = null;
            _devices.Clear();
        }

        if (watcher is not null)
        {
            watcher.Added -= OnAdded;
            watcher.Updated -= OnUpdated;
            watcher.Removed -= OnRemoved;
            watcher.EnumerationCompleted -= OnEnumerationCompleted;
            if (watcher.Status is DeviceWatcherStatus.Started or DeviceWatcherStatus.EnumerationCompleted)
            {
                watcher.Stop();
            }
        }

        return Task.CompletedTask;
    }

    public async Task<int?> GetBatteryAsync(string deviceId)
    {
        Guid? container;
        lock (_lock)
        {
            container = _devices.TryGetValue(deviceId, out var entry) ? entry.ContainerId : null;
        }

        if (container is null)
        {
            return null;
        }

        try
        {
            // Os nós de dispositivo Bluetooth (BTHENUM) do mesmo aparelho compartilham o ContainerId.
            var nodes = await Task.Run(async () => await DeviceInformation.FindAllAsync(
                "System.Devices.DeviceInstanceId:~<\"BTHENUM\"", [BatteryProperty, ContainerProperty], DeviceInformationKind.Device));
            foreach (var node in nodes)
            {
                if (node.Properties.TryGetValue(ContainerProperty, out var id) && id is Guid guid && guid == container
                    && node.Properties.TryGetValue(BatteryProperty, out var battery) && battery is not null)
                {
                    var percent = Convert.ToInt32(battery, System.Globalization.CultureInfo.InvariantCulture);
                    if (percent is >= 0 and <= 100)
                    {
                        return percent;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogInformation("Bateria Bluetooth indisponível: {Reason}", ex.Message);
        }

        return null;
    }

    private void OnAdded(DeviceWatcher sender, DeviceInformation device)
    {
        var entry = new Entry(
            string.IsNullOrWhiteSpace(device.Name) ? "Dispositivo Bluetooth" : device.Name,
            ReadConnected(device.Properties) ?? false,
            device.Properties.TryGetValue(AepContainerProperty, out var container) && container is Guid id ? id : null);

        bool announce;
        lock (_lock)
        {
            if (!ReferenceEquals(sender, _watcher))
            {
                return;
            }

            _devices[device.Id] = entry;

            // Pareado e conectado depois da enumeração inicial: é uma conexão nova.
            announce = _enumerated && entry.IsConnected;
        }

        if (announce)
        {
            Raise(new BluetoothDeviceInfo(device.Id, entry.Name, true));
        }
    }

    private void OnUpdated(DeviceWatcher sender, DeviceInformationUpdate update)
    {
        if (ReadConnected(update.Properties) is not { } connected)
        {
            return;
        }

        BluetoothDeviceInfo? changed = null;
        lock (_lock)
        {
            if (ReferenceEquals(sender, _watcher) && _devices.TryGetValue(update.Id, out var entry) && entry.IsConnected != connected)
            {
                _devices[update.Id] = entry with { IsConnected = connected };
                changed = new BluetoothDeviceInfo(update.Id, entry.Name, connected);
            }
        }

        if (changed is not null)
        {
            Raise(changed);
        }
    }

    private void OnRemoved(DeviceWatcher sender, DeviceInformationUpdate update)
    {
        lock (_lock)
        {
            if (ReferenceEquals(sender, _watcher))
            {
                _devices.Remove(update.Id);
            }
        }
    }

    private void OnEnumerationCompleted(DeviceWatcher sender, object args)
    {
        lock (_lock)
        {
            if (ReferenceEquals(sender, _watcher))
            {
                _enumerated = true;
            }
        }
    }

    private void Raise(BluetoothDeviceInfo device)
    {
        try
        {
            ConnectionChanged?.Invoke(this, device);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Falha ao processar a mudança de conexão Bluetooth.");
        }
    }

    private static bool? ReadConnected(IReadOnlyDictionary<string, object> properties) =>
        properties.TryGetValue(IsConnectedProperty, out var value) && value is bool connected ? connected : null;

    private sealed record Entry(string Name, bool IsConnected, Guid? ContainerId);
}
