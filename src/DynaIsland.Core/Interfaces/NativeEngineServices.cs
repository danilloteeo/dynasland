// Contrato do motor nativo (C++): ganchos globais do Windows numa thread própria, fora da interface.

namespace DynaIsland.Core.Interfaces;

public enum NativeEventType
{
    /// <summary>Botão do mouse apertado; <see cref="NativeEvent.X"/>/<see cref="NativeEvent.Y"/> em pixels físicos.</summary>
    MouseDown = 1,

    EscapeDown = 2,

    /// <summary>Outra janela assumiu o primeiro plano (<see cref="NativeEvent.Window"/> é o HWND).</summary>
    ForegroundChanged = 3,
}

public readonly record struct NativeEvent(NativeEventType Type, int X, int Y, long Window, int Button);

/// <summary>
/// Motor nativo. Se a DLL não estiver disponível (ausente, versão diferente, falha ao iniciar) <see cref="IsAvailable"/>
/// fica falso e quem usa deve cair na implementação em C#.
/// </summary>
public interface INativeEngine : IDisposable
{
    bool IsAvailable { get; }

    /// <summary>Disparado na thread de entrega do motor, nunca na da interface: quem escuta deve só enfileirar e voltar.</summary>
    event Action<NativeEvent>? EventReceived;

    /// <summary>Liga/desliga os ganchos globais de mouse e teclado (Esc). <c>false</c> se o motor não estiver disponível.</summary>
    bool SetInputHooks(bool enabled);
}
