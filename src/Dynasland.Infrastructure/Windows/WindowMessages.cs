namespace Dynasland.Infrastructure.Windows;

/// <summary>Mensagens Win32 tratadas pelas janelas do Dynasland.</summary>
public static class WindowMessages
{
    public const int WM_SETTINGCHANGE = 0x001A;
    public const int WM_MOUSEACTIVATE = 0x0021;
    public const int WM_DISPLAYCHANGE = 0x007E;

    /// <summary>Resposta a WM_MOUSEACTIVATE: não ativa a janela, mas entrega o clique.</summary>
    public const int MA_NOACTIVATE = 3;

    /// <summary>wParam de WM_SETTINGCHANGE quando a área útil (barra de tarefas) muda.</summary>
    public const int SPI_SETWORKAREA = 0x002F;
}
