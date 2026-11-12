using Dynasland.Core.Modules;

namespace Dynasland.Modules.Common;

public enum DockItemKind
{
    /// <summary>O módulo não tem atividade agora: o clique chama o atalho dele.</summary>
    Shortcut,

    /// <summary>O módulo tem uma atividade em andamento: o clique abre essa atividade.</summary>
    Activity,
}

/// <summary>Atividade em andamento, reduzida ao que a dock precisa.</summary>
public sealed record DockActivity(string ActivityId, string ModuleId);

/// <summary>Um ícone da dock.</summary>
public sealed record DockItem(
    string ModuleId,
    string Glyph,
    string Label,
    bool IsActive,
    DockItemKind Kind,
    string? ShortcutId,
    string? ActivityId);

/// <summary>Monta os ícones da dock: um por módulo, na ordem fixa dos módulos.</summary>
public static class DockItems
{
    private static readonly string[] ModuleOrder =
    [
        "media", "volume", "brightness", "timer", "weather", "calendar", "downloads", "bluetooth",
        "battery", "capture", "clipboard", "notifications", "system", "gaming", "clock", "water",
    ];

    /// <param name="shortcuts">Atalhos registrados pelos módulos ligados.</param>
    /// <param name="activities">Atividades em andamento (contínuas e a aberta).</param>
    /// <param name="focusedModuleId">Módulo da atividade em foco; o ícone dele fica ativo.</param>
    public static IReadOnlyList<DockItem> Build(
        IReadOnlyList<IslandShortcut> shortcuts,
        IReadOnlyList<DockActivity> activities,
        string? focusedModuleId)
    {
        var byModule = new Dictionary<string, DockItem>(StringComparer.Ordinal);

        foreach (var activity in activities)
        {
            if (byModule.ContainsKey(activity.ModuleId) || Describe(activity.ModuleId) is not { } icon)
            {
                continue;
            }

            byModule[activity.ModuleId] = new DockItem(
                activity.ModuleId, icon.Glyph, icon.Name, activity.ModuleId == focusedModuleId,
                DockItemKind.Activity, ShortcutId: null, activity.ActivityId);
        }

        foreach (var shortcut in shortcuts)
        {
            if (byModule.ContainsKey(shortcut.ModuleId) || Describe(shortcut.ModuleId) is not { } icon)
            {
                continue;
            }

            byModule[shortcut.ModuleId] = new DockItem(
                shortcut.ModuleId, icon.Glyph, icon.Name, shortcut.ModuleId == focusedModuleId,
                DockItemKind.Shortcut, shortcut.Id, ActivityId: null);
        }

        return [.. byModule.Values.OrderBy(i => OrderOf(i.ModuleId))];
    }

    /// <summary>O módulo "app" (configurações, atualização) não é um módulo da dock.</summary>
    private static PageIcon? Describe(string moduleId) =>
        moduleId == PageIcons.AppModuleId ? null : PageIcons.For(moduleId);

    private static int OrderOf(string moduleId)
    {
        var index = Array.IndexOf(ModuleOrder, moduleId);
        return index < 0 ? ModuleOrder.Length : index;
    }
}
