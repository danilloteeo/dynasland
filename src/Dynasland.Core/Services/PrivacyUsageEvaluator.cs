using Dynasland.Core.Models;

namespace Dynasland.Core.Services;

/// <summary>Decide, a partir dos registros de uso do Windows, se o microfone ou a câmera estão em uso agora.</summary>
public static class PrivacyUsageEvaluator
{
    /// <summary>
    /// Em uso: já começou e o fim ainda não foi gravado. O Windows zera o fim a cada início e
    /// grava o horário (maior ou igual ao início) quando o aplicativo solta o dispositivo.
    /// </summary>
    public static bool IsInUse(long start, long stop) => start > 0 && stop == 0;

    public static PrivacyUsage Evaluate(IEnumerable<PrivacyUsageEntry> entries)
    {
        var microphone = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        var camera = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in entries)
        {
            if (!IsInUse(entry.Start, entry.Stop))
            {
                continue;
            }

            var name = AppNameFromKey(entry.Key);
            (entry.Capability == PrivacyCapability.Camera ? camera : microphone).Add(name);
        }

        return microphone.Count == 0 && camera.Count == 0
            ? PrivacyUsage.None
            : new PrivacyUsage(microphone.Count > 0, camera.Count > 0, [.. microphone], [.. camera]);
    }

    /// <summary>
    /// Nome legível da chave: aplicativos desktop (<c>C:#Program Files#...#app.exe</c>) viram o nome do
    /// arquivo; empacotados (<c>Microsoft.Office.OneNote_8wekyb3d8bbwe</c>) perdem o sufixo do editor e o prefixo.
    /// </summary>
    public static string AppNameFromKey(string key)
    {
        if (key.Contains('#'))
        {
            var lastSeparator = key.LastIndexOf('#');
            return key[(lastSeparator + 1)..];
        }

        var name = key;
        var underscore = name.LastIndexOf('_');
        if (underscore > 0)
        {
            name = name[..underscore];
        }

        var dot = name.LastIndexOf('.');
        return dot >= 0 && dot < name.Length - 1 ? name[(dot + 1)..] : name;
    }
}
