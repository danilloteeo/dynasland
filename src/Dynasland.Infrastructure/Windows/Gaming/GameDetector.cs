using System.Diagnostics;
using System.Runtime.InteropServices;
using Dynasland.Core.Interfaces;

namespace Dynasland.Infrastructure.Windows.Gaming;

/// <summary>
/// Verifica a janela em primeiro plano a cada 3 segundos (uma chamada barata ao Windows) e decide se é
/// um jogo pela pasta do executável ou por ocupar a tela inteira.
/// </summary>
public sealed class GameDetector(TimeProvider time) : IGameDetector, IDisposable
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(3);

    /// <summary>Trechos de caminho das bibliotecas das lojas de jogos.</summary>
    internal static readonly string[] GameFolders =
    [
        @"\steamapps\common\",
        @"\Epic Games\",
        @"\XboxGames\",
        @"\WindowsApps\Microsoft.Xbox",
        @"\GOG Galaxy\Games\",
        @"\GOG Games\",
        @"\Riot Games\",
        @"\Ubisoft Game Launcher\games\",
        @"\EA Games\",
        @"\Origin Games\",
        @"\Battle.net\",
        @"\Blizzard Entertainment\",
        @"\Rockstar Games\",
    ];

    /// <summary>Programas comuns em tela cheia que não são jogos.</summary>
    internal static readonly HashSet<string> NotGames = new(StringComparer.OrdinalIgnoreCase)
    {
        "chrome", "msedge", "firefox", "opera", "opera_gx", "brave", "vivaldi", "iexplore",
        "vlc", "mpc-hc", "mpc-hc64", "mpc-be64", "PotPlayerMini64", "wmplayer", "Video.UI", "Microsoft.Media.Player",
        "POWERPNT", "explorer", "ApplicationFrameHost", "mstsc", "Dynasland", "obs64", "Spotify", "Netflix",
        "steamwebhelper", "EpicGamesLauncher", "steam", "Discord", "devenv", "Code",
    };

    private readonly Lock _lock = new();
    private ITimer? _timer;

    public GameSession? Current { get; private set; }

    public event EventHandler<GameSession?>? Changed;

    public void Start()
    {
        lock (_lock)
        {
            _timer ??= time.CreateTimer(_ => Check(), null, TimeSpan.FromSeconds(1), CheckInterval);
        }
    }

    public void Stop()
    {
        lock (_lock)
        {
            _timer?.Dispose();
            _timer = null;
        }

        if (Current is not null)
        {
            Current = null;
            Changed?.Invoke(this, null);
        }
    }

    public void Dispose() => Stop();

    /// <summary>Regra de decisão (separada para testes).</summary>
    internal static bool IsGame(string processName, string? path, bool fullscreen)
    {
        if (NotGames.Contains(processName))
        {
            return false;
        }

        if (path is not null && GameFolders.Any(folder => path.Contains(folder, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        return fullscreen;
    }

    private void Check()
    {
        var window = GetForegroundWindow();
        GameSession? detected = null;
        if (window != IntPtr.Zero)
        {
            GetWindowThreadProcessId(window, out var processId);
            if (processId != 0 && processId != Environment.ProcessId)
            {
                detected = Inspect((int)processId, window);
            }
        }

        if (detected?.ProcessId == Current?.ProcessId)
        {
            return;
        }

        Current = detected;
        Changed?.Invoke(this, Current);
    }

    private GameSession? Inspect(int processId, IntPtr window)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            string? path = null;
            try
            {
                path = process.MainModule?.FileName;
            }
            catch (System.ComponentModel.Win32Exception)
            {
                // Processo protegido ou elevado: decide só pela tela cheia.
            }

            if (!IsGame(process.ProcessName, path, IsFullscreen(window)))
            {
                return null;
            }

            var name = process.MainWindowTitle is { Length: > 0 } title ? title : process.ProcessName;
            return new GameSession(processId, name, path ?? process.ProcessName, time.GetUtcNow());
        }
        catch (ArgumentException)
        {
            return null; // processo encerrado
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    private static bool IsFullscreen(IntPtr window)
    {
        if (!GetWindowRect(window, out var rect))
        {
            return false;
        }

        var monitor = MonitorFromWindow(window, 2);
        var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        return GetMonitorInfo(monitor, ref info)
            && rect.Left <= info.Monitor.Left && rect.Top <= info.Monitor.Top
            && rect.Right >= info.Monitor.Right && rect.Bottom >= info.Monitor.Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left, Top, Right, Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo
    {
        public int Size;
        public Rect Monitor;
        public Rect Work;
        public uint Flags;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint processId);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hwnd, out Rect rect);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint flags);

    [DllImport("user32.dll")]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);
}
