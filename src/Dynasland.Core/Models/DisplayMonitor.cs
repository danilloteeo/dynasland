namespace Dynasland.Core.Models;

/// <summary>Monitor conectado, com geometria em pixels físicos.</summary>
/// <param name="DeviceName">Nome do dispositivo GDI (ex.: <c>\\.\DISPLAY1</c>).</param>
/// <param name="Bounds">Área total do monitor.</param>
/// <param name="WorkArea">Área útil (sem a barra de tarefas).</param>
/// <param name="IsPrimary">Indica o monitor principal do Windows.</param>
/// <param name="Dpi">DPI efetivo do monitor (96 = 100%).</param>
public sealed record DisplayMonitor(string DeviceName, PixelRect Bounds, PixelRect WorkArea, bool IsPrimary, uint Dpi)
{
    public double Scale => Dpi / 96.0;

    /// <summary>Número do monitor extraído do nome do dispositivo, ou <c>null</c> se não houver.</summary>
    public int? DisplayNumber
    {
        get
        {
            var digits = new string(DeviceName.Reverse().TakeWhile(char.IsDigit).Reverse().ToArray());
            return int.TryParse(digits, out var number) ? number : null;
        }
    }
}
