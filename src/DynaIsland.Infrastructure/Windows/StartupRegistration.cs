using DynaIsland.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;

namespace DynaIsland.Infrastructure.Windows;

/// <summary>
/// Iniciar com o Windows via HKCU\Software\Microsoft\Windows\CurrentVersion\Run.
/// O estado é sempre lido do registro, então reflete também mudanças feitas no Gerenciador de Tarefas.
/// </summary>
public sealed class StartupRegistration(ILogger<StartupRegistration> logger) : IStartupRegistration
{
    public const string StartupArgument = "--inicializacao";

    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "DynaIsland";

    private static string Command => $"\"{Environment.ProcessPath}\" {StartupArgument}";

    /// <summary>Valor da chave Run até a 1.7.1, quando o aplicativo se chamava Dynasland.</summary>
    private const string LegacyValueName = "Dynasland";

    /// <summary>
    /// Quem tinha "Iniciar com o Windows" ligado no Dynasland continua com ele ligado: a entrada antiga
    /// (que aponta para um executável que não existe mais) vira a nova. Sem efeito se não havia entrada.
    /// </summary>
    public void MigrateLegacyEntry()
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);
            if (key.GetValue(LegacyValueName) is null)
            {
                return;
            }

            if (key.GetValue(ValueName) is null)
            {
                key.SetValue(ValueName, Command);
            }

            key.DeleteValue(LegacyValueName, throwOnMissingValue: false);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Não foi possível migrar a inicialização com o Windows do nome antigo.");
        }
    }

    public bool IsEnabled
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
            return key?.GetValue(ValueName) is string value && value.Contains(Path.GetFileName(Environment.ProcessPath ?? "DynaIsland.exe"), StringComparison.OrdinalIgnoreCase);
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
