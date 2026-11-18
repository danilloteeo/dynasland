using System.Runtime.InteropServices;
using DynaIsland.Core.Interfaces;
using DynaIsland.Infrastructure.Native;
using Microsoft.Extensions.Logging.Abstractions;

namespace DynaIsland.Tests.Infrastructure;

/// <summary>
/// Integração C# / C++: carrega a DLL de verdade (gerada por tools\Build-NativeEngine.ps1 e copiada para a saída).
/// Não gera cliques nem teclas; só liga e desliga os ganchos.
/// </summary>
public sealed class NativeEngineTests
{
    private static bool DllPresent => File.Exists(Path.Combine(AppContext.BaseDirectory, "DynaIslandEngine.dll"));

    [Fact]
    public void Event_struct_matches_the_native_layout()
    {
        // di_event: 4 + 4 + 4 + 4 + 8 + 4 + 4 bytes, ver dynaisland_engine.h.
        Assert.Equal(32, Marshal.SizeOf<NativeMethods.Event>());
        Assert.Equal(16, (int)Marshal.OffsetOf<NativeMethods.Event>(nameof(NativeMethods.Event.Window)));
        Assert.Equal(24, (int)Marshal.OffsetOf<NativeMethods.Event>(nameof(NativeMethods.Event.Button)));
    }

    [Fact]
    public void Expected_api_version_matches_the_dll()
    {
        if (!DllPresent)
        {
            return; // sem toolchain C++ o motor não foi compilado: o aplicativo roda sem ele
        }

        Assert.Equal(NativeEngineService.ExpectedApiVersion, NativeMethods.ApiVersion());
    }

    [Fact]
    public void Engine_starts_toggles_input_hooks_and_stops()
    {
        if (!DllPresent)
        {
            return;
        }

        using var engine = new NativeEngineService(NullLogger<NativeEngineService>.Instance);

        Assert.True(engine.IsAvailable);
        Assert.True(engine.SetInputHooks(true));
        Assert.True(engine.SetInputHooks(false));
    }

    [Fact]
    public void Engine_can_be_recreated_after_dispose()
    {
        if (!DllPresent)
        {
            return;
        }

        for (var i = 0; i < 3; i++)
        {
            using var engine = new NativeEngineService(NullLogger<NativeEngineService>.Instance);
            Assert.True(engine.IsAvailable);
        }
    }

    [Fact]
    public void Calls_after_dispose_report_unavailable_instead_of_throwing()
    {
        if (!DllPresent)
        {
            return;
        }

        var engine = new NativeEngineService(NullLogger<NativeEngineService>.Instance);
        engine.Dispose();

        Assert.False(engine.IsAvailable);
        Assert.False(engine.SetInputHooks(true));
        engine.Dispose(); // idempotente
    }

    [Fact]
    public void Second_instance_does_not_start_while_the_first_is_running()
    {
        if (!DllPresent)
        {
            return;
        }

        using var first = new NativeEngineService(NullLogger<NativeEngineService>.Instance);
        using var second = new NativeEngineService(NullLogger<NativeEngineService>.Instance);

        Assert.True(first.IsAvailable);
        Assert.False(second.IsAvailable); // o motor é um só por processo (DI_ERR_ALREADY_STARTED)
    }
}
