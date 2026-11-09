using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using Microsoft.Extensions.Logging;

namespace Dynasland.Infrastructure.Logging;

/// <summary>
/// Log em arquivo texto (%LocalAppData%\Dynasland\logs\dynasland.log), para diagnosticar problemas
/// relatados pelo usuário. Ao passar de <see cref="MaxBytes"/>, o arquivo vira <c>.1.log</c> e recomeça.
/// </summary>
public sealed class FileLoggerProvider : ILoggerProvider
{
    public const long MaxBytes = 1024 * 1024;

    private readonly string _path;
    private readonly LogLevel _minimumLevel;
    private readonly Lock _writeLock = new();
    private readonly ConcurrentDictionary<string, FileLogger> _loggers = new();

    public FileLoggerProvider(string path, LogLevel minimumLevel = LogLevel.Information)
    {
        _path = path;
        _minimumLevel = minimumLevel;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
    }

    public static string DefaultPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Dynasland", "logs", "dynasland.log");

    public ILogger CreateLogger(string categoryName) => _loggers.GetOrAdd(categoryName, name => new FileLogger(this, name));

    public void Dispose() => _loggers.Clear();

    private void Write(string category, LogLevel level, string message, Exception? exception)
    {
        var line = new StringBuilder()
            .Append(DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture))
            .Append(' ').Append(level.ToString().ToUpperInvariant()[..4])
            .Append(' ').Append(category[(category.LastIndexOf('.') + 1)..])
            .Append(": ").Append(message);
        if (exception is not null)
        {
            line.AppendLine().Append(exception);
        }

        lock (_writeLock)
        {
            try
            {
                var file = new FileInfo(_path);
                if (file.Exists && file.Length > MaxBytes)
                {
                    File.Move(_path, Path.ChangeExtension(_path, ".1.log"), overwrite: true);
                }

                File.AppendAllText(_path, line.AppendLine().ToString());
            }
            catch (IOException)
            {
                // Log nunca pode derrubar o aplicativo.
            }
        }
    }

    private sealed class FileLogger(FileLoggerProvider provider, string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= provider._minimumLevel && logLevel != LogLevel.None;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (IsEnabled(logLevel))
            {
                provider.Write(category, logLevel, formatter(state, exception), exception);
            }
        }
    }
}
