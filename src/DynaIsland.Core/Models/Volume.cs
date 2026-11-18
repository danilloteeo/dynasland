namespace DynaIsland.Core.Models;

/// <param name="Level">Volume mestre de 0 a 1.</param>
/// <param name="DeviceName">Nome do dispositivo de saída padrão, quando disponível.</param>
public sealed record VolumeState(float Level, bool IsMuted, string? DeviceName)
{
    public int Percent => (int)Math.Round(Math.Clamp(Level, 0f, 1f) * 100);
}

public enum VolumeChangeReason
{
    /// <summary>Alterado fora do DynaIsland (teclas de volume, mixer, outro app).</summary>
    External,

    /// <summary>Alterado pelo próprio DynaIsland.</summary>
    Own,

    /// <summary>O dispositivo de saída padrão mudou.</summary>
    DeviceChanged,
}

public sealed record VolumeChangedEventArgs(VolumeState State, VolumeChangeReason Reason);
