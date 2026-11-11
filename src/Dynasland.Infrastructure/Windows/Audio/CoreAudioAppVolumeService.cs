using System.Diagnostics;
using System.Runtime.InteropServices;
using Dynasland.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace Dynasland.Infrastructure.Windows.Audio;

/// <summary>
/// Volume por aplicativo no dispositivo de saída padrão (sessões de áudio do Core Audio, as mesmas do
/// misturador de volume do Windows). Sessões do mesmo programa (ex.: várias abas do navegador) são
/// tratadas como uma só. Cada chamada enumera as sessões de novo, numa thread do pool (MTA).
/// </summary>
public sealed class CoreAudioAppVolumeService(ILogger<CoreAudioAppVolumeService> logger) : IAppVolumeService
{
    private const int SessionStateActive = 1;
    private const int SessionStateExpired = 2;

    public Task<IReadOnlyList<AppAudioSession>> GetSessionsAsync() => Task.Run<IReadOnlyList<AppAudioSession>>(() =>
    {
        var groups = new Dictionary<string, (float Level, bool AllMuted, bool Active)>(StringComparer.OrdinalIgnoreCase);
        try
        {
            ForEachSession((process, state, volume) =>
            {
                volume.GetMasterVolume(out var level);
                volume.GetMute(out var muted);
                var active = state == SessionStateActive;
                groups[process] = groups.TryGetValue(process, out var group)
                    ? (Math.Max(group.Level, level), group.AllMuted && muted, group.Active || active)
                    : (level, muted, active);
            });
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Falha ao ler o volume dos aplicativos.");
        }

        return [.. groups
            .Select(g => new AppAudioSession(g.Key, AppDisplayNameResolver.Resolve(g.Key + ".exe"), g.Value.Level, g.Value.AllMuted, g.Value.Active))
            .OrderByDescending(s => s.IsActive)
            .ThenBy(s => s.Name, StringComparer.CurrentCultureIgnoreCase)];
    });

    public Task SetLevelAsync(string id, float level) => Apply(id, volume =>
    {
        var context = Guid.Empty;
        volume.SetMasterVolume(Math.Clamp(level, 0f, 1f), ref context);
    });

    public Task SetMutedAsync(string id, bool muted) => Apply(id, volume =>
    {
        var context = Guid.Empty;
        volume.SetMute(muted, ref context);
    });

    private Task Apply(string id, Action<ISimpleAudioVolume> change) => Task.Run(() =>
    {
        try
        {
            ForEachSession((process, _, volume) =>
            {
                if (string.Equals(process, id, StringComparison.OrdinalIgnoreCase))
                {
                    change(volume);
                }
            });
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Falha ao ajustar o volume do aplicativo {App}.", id);
        }
    });

    /// <summary>Percorre as sessões de aplicativos (sem sons do sistema, sem o próprio Dynasland e sem as expiradas).</summary>
    private static void ForEachSession(Action<string, int, ISimpleAudioVolume> action)
    {
        var names = new Dictionary<uint, string?>();
        var enumerator = (IMMDeviceEnumerator)new MMDeviceEnumeratorComObject();
        try
        {
            if (enumerator.GetDefaultAudioEndpoint(EDataFlow.Render, ERole.Multimedia, out var device) != 0 || device is null)
            {
                return;
            }

            try
            {
                var iid = typeof(IAudioSessionManager2).GUID;
                if (device.Activate(ref iid, CoreAudioNative.ClsCtxAll, IntPtr.Zero, out var instance) != 0)
                {
                    return;
                }

                var manager = (IAudioSessionManager2)instance;
                try
                {
                    if (manager.GetSessionEnumerator(out var sessions) != 0 || sessions is null)
                    {
                        return;
                    }

                    try
                    {
                        sessions.GetCount(out var count);
                        for (var i = 0; i < count; i++)
                        {
                            if (sessions.GetSession(i, out var session) != 0 || session is null)
                            {
                                continue;
                            }

                            try
                            {
                                Visit(session, names, action);
                            }
                            finally
                            {
                                Marshal.ReleaseComObject(session);
                            }
                        }
                    }
                    finally
                    {
                        Marshal.ReleaseComObject(sessions);
                    }
                }
                finally
                {
                    Marshal.ReleaseComObject(manager);
                }
            }
            finally
            {
                Marshal.ReleaseComObject(device);
            }
        }
        finally
        {
            Marshal.ReleaseComObject(enumerator);
        }
    }

    private static void Visit(IAudioSessionControl2 session, Dictionary<uint, string?> names, Action<string, int, ISimpleAudioVolume> action)
    {
        if (session.IsSystemSoundsSession() == 0
            || session.GetState(out var state) != 0 || state == SessionStateExpired
            || session.GetProcessId(out var processId) != 0 || processId == 0 || processId == Environment.ProcessId)
        {
            return;
        }

        if (!names.TryGetValue(processId, out var name))
        {
            name = ProcessName(processId);
            names[processId] = name;
        }

        // A mesma sessão expõe o controle de volume (QueryInterface no mesmo objeto).
        if (name is not null && session is ISimpleAudioVolume volume)
        {
            action(name, state, volume);
        }
    }

    private static string? ProcessName(uint processId)
    {
        try
        {
            using var process = Process.GetProcessById((int)processId);
            return process.ProcessName;
        }
        catch (Exception)
        {
            // Processo encerrado entre a enumeração e a consulta.
            return null;
        }
    }
}
