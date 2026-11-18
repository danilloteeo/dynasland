using System.Runtime.InteropServices;
using DynaIsland.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace DynaIsland.Infrastructure.Native;

/// <summary>
/// Carrega <c>DynaIslandEngine.dll</c> (src/NativeEngine) e a mantém iniciada enquanto o aplicativo roda.
/// Qualquer falha ao carregar ou iniciar deixa o serviço indisponível, sem derrubar o aplicativo.
/// </summary>
public sealed class NativeEngineService : INativeEngine
{
    /// <summary>Versão da API nativa com que este código foi escrito (DI_API_VERSION em dynaisland_engine.h).</summary>
    public const uint ExpectedApiVersion = 1;

    private readonly ILogger<NativeEngineService> _logger;

    // Os delegates ficam em campos: o motor guarda os ponteiros, e o coletor não pode recolhê-los.
    private readonly NativeMethods.EventCallback _onEvent;
    private readonly NativeMethods.LogCallback _onLog;
    private bool _started;

    public NativeEngineService(ILogger<NativeEngineService> logger)
    {
        _logger = logger;
        _onEvent = OnEvent;
        _onLog = OnLog;
        Start();
    }

    public bool IsAvailable => _started;

    public event Action<NativeEvent>? EventReceived;

    public bool SetInputHooks(bool enabled)
    {
        if (!_started)
        {
            return false;
        }

        try
        {
            var result = NativeMethods.SetInputHooks(enabled ? 1 : 0);
            if (result != NativeResult.Ok)
            {
                _logger.LogWarning("Motor nativo: não foi possível {Action} os ganchos de entrada ({Result}).", enabled ? "ligar" : "desligar", Describe(result));
            }

            return result == NativeResult.Ok;
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException)
        {
            _logger.LogWarning(ex, "Motor nativo indisponível ao ajustar os ganchos de entrada.");
            return false;
        }
    }

    public void Dispose()
    {
        if (!_started)
        {
            return;
        }

        _started = false;
        try
        {
            NativeMethods.Stop();
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException)
        {
            _logger.LogWarning(ex, "Motor nativo: falha ao parar.");
        }
    }

    private void Start()
    {
        try
        {
            var dllVersion = NativeMethods.ApiVersion();
            if (dllVersion != ExpectedApiVersion)
            {
                _logger.LogWarning("Motor nativo ignorado: a DLL é da API {Dll}, o aplicativo espera a {Expected}.", dllVersion, ExpectedApiVersion);
                return;
            }

            var result = NativeMethods.Start(ExpectedApiVersion, _onEvent, IntPtr.Zero, _onLog, IntPtr.Zero);
            if (result != NativeResult.Ok)
            {
                _logger.LogWarning("Motor nativo não iniciou: {Result}.", Describe(result));
                return;
            }

            _started = true;
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException)
        {
            // Esperado na versão portátil antiga ou se a DLL foi apagada: o aplicativo usa os ganchos em C#.
            _logger.LogInformation(ex, "Motor nativo não encontrado; usando a implementação em C#.");
        }
    }

    private void OnEvent(IntPtr nativeEvent, IntPtr userData)
    {
        try
        {
            var handler = EventReceived;
            if (handler is null || nativeEvent == IntPtr.Zero)
            {
                return;
            }

            var e = Marshal.PtrToStructure<NativeMethods.Event>(nativeEvent);
            if (e.Size < Marshal.SizeOf<NativeMethods.Event>() || !Enum.IsDefined((NativeEventType)e.Type))
            {
                return; // evento de uma versão que este código não conhece: ignora em vez de adivinhar
            }

            handler(new NativeEvent((NativeEventType)e.Type, e.X, e.Y, unchecked((long)e.Window), (int)e.Button));
        }
        catch (Exception ex)
        {
            // Não pode escapar para código nativo.
            _logger.LogWarning(ex, "Motor nativo: erro ao tratar um evento.");
        }
    }

    private void OnLog(int level, IntPtr message, IntPtr userData)
    {
        try
        {
            var text = Marshal.PtrToStringUTF8(message) ?? string.Empty;
            _logger.Log(level switch
            {
                0 => LogLevel.Debug,
                1 => LogLevel.Information,
                2 => LogLevel.Warning,
                _ => LogLevel.Error,
            }, "Motor nativo: {Message}", text);
        }
        catch (Exception)
        {
            // O log do motor nunca deve causar falha.
        }
    }

    private static string Describe(NativeResult result) =>
        Marshal.PtrToStringAnsi(NativeMethods.ResultMessage(result)) ?? result.ToString();
}
