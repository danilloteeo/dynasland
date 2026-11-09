using Dynasland.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;

namespace Dynasland.Infrastructure.Windows;

/// <summary>
/// Iniciar com o Windows via HKCU\Software\Microsoft\Windows\CurrentVersion\Run.
/// O estado é sempre lido do registro, então reflete também mudanças feitas no Gerenciador de Tarefas.
/// </summary>
public sealed class StartupRegistration(ILogger<StartupRegistration> logger) : IStartupRegistration
{
    public const string StartupArgument = "--inicializacao";

    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "Dynasland";

    private static string Command => $"\"{Environment.ProcessPath}\" {StartupArgument}";

    public bool IsEnabled
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
            return key?.GetValue(ValueName) is string value && value.Contains(Path.GetFileName(Environment.ProcessPath ?? "Dynasland.exe"), StringComparison.OrdinalIgnoreCase);
        }
    }

    public void SetEnabled(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);
            if (enabled)
            {
                key.SetValue(ValueName, Command);
            }
            else
            {
                key.DeleteValue(ValueName, throwOnMissingValue: false);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Não foi possível alterar a inicialização com o Windows.");
        }
    }
}
