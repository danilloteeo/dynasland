using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using DynaIsland.Core.Activities;
using DynaIsland.Core.Interfaces;
using DynaIsland.Core.Layout;
using DynaIsland.Core.Models;
using DynaIsland.Modules.Common;

namespace DynaIsland.Modules.Bluetooth;

/// <summary>Uma linha do painel: dispositivo conectado e a bateria dele, quando o Windows informa.</summary>
public sealed class BluetoothDeviceRow(string name, BluetoothDeviceKind kind, int? battery, int lowThreshold)
{
    public const string NoReading = "sem leitura";

    public string Name => name;

    public string Glyph => kind switch
    {
        BluetoothDeviceKind.Mouse => VectorIcons.Mouse,
        BluetoothDeviceKind.Keyboard => VectorIcons.Keyboard,
        BluetoothDeviceKind.Headset => VectorIcons.Headphones,
        BluetoothDeviceKind.Gamepad => VectorIcons.Game,
        _ => VectorIcons.Bluetooth,
    };

    public bool HasBattery => battery is not null;

    public double Value => battery ?? 0;

    public string BatteryText => battery is { } percent ? $"{percent}%" : NoReading;

    public bool IsLow => battery is { } percent && percent <= lowThreshold;
}

/// <summary>Painel com os dispositivos Bluetooth conectados e a bateria de cada um (mouse, teclado, fone...).</summary>
public sealed partial class BluetoothDevicesContent(Action<IslandState?> presentationChanged) : ObservableObject, IActivityContent
{
    private const int RowHeight = 34;

    [ObservableProperty]
    private bool _isEmpty = true;

    [ObservableProperty]
    private string _emptyText = "Procurando dispositivos…";

    public ObservableCollection<BluetoothDeviceRow> Devices { get; } = [];

    public string Glyph => Glyphs.Bluetooth;

    public void Apply(IReadOnlyList<BluetoothDeviceRow> rows)
    {
        Devices.Clear();
        foreach (var row in rows)
        {
            Devices.Add(row);
        }

        IsEmpty = rows.Count == 0;
        EmptyText = "Nenhum dispositivo Bluetooth conectado";
    }

    public IslandDimensions? GetPreferredDimensions(IslandState state) =>
        state == IslandState.Interactive
            ? new IslandDimensions(360, 40 + Math.Clamp(Devices.Count, 1, 6) * RowHeight, 32)
            : null;

    public void OnPresentationChanged(IslandState? state) => presentationChanged(state);
}
