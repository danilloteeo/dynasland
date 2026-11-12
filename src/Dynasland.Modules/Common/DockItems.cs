using Dynasland.Core.Models;
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
    string? ActivityId,
    DockBadge? Badge = null);

/// <summary>Monta os ícones da dock: um por módulo, na ordem fixa dos módulos.</summary>
public static class DockItems
{
    private static readonly string[] ModuleOrder =
    [
        "media", "volume", "brightness", "quotes", "scores", "timer", "weather", "calendar", "downloads", "bluetooth",
        "battery", "capture", "clipboard", "notifications", "system", "gaming", "clock", "water",
    ];

    /// <param name="shortcuts">Atalhos registrados pelos módulos ligados.</param>
    /// <param name="activities">Atividades em andamento (contínuas e a aberta).</param>
    /// <param name="focusedModuleId">Módulo da atividade em foco; o ícone dele fica ativo.</param>
    /// <param name="hiddenModules">Módulos que o usuário escondeu da dock; só somem se não tiverem atividade.</param>
    /// <param name="badges">Selos (ponto, anel de progresso) por módulo.</param>
    public static IReadOnlyList<DockItem> Build(
        IReadOnlyList<IslandShortcut> shortcuts,
        IReadOnlyList<DockActivity> activities,
        string? focusedModuleId,
        IReadOnlySet<string>? hiddenModules = null,
        IReadOnlyDictionary<string, DockBadge>? badges = null)
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
                DockItemKind.Activity, ShortcutId: null, activity.ActivityId, BadgeFor(badges, activity.ModuleId));
        }

        foreach (var shortcut in shortcuts)
        {
            if (byModule.ContainsKey(shortcut.ModuleId) || hiddenModules?.Contains(shortcut.ModuleId) == true
                || Describe(shortcut.ModuleId) is not { } icon)
            {
                continue;
            }

            byModule[shortcut.ModuleId] = new DockItem(
                shortcut.ModuleId, icon.Glyph, icon.Name, shortcut.ModuleId == focusedModuleId,
                DockItemKind.Shortcut, shortcut.Id, ActivityId: null, BadgeFor(badges, shortcut.ModuleId));
        }

        return [.. byModule.Values.OrderBy(i => OrderOf(i.ModuleId))];
    }

    /// <summary>Módulo do relógio, o indicador do repouso.</summary>
    public const string IdleModuleId = "clock";

    /// <summary>
    /// Qual ícone fica ativo (pílula branca). No compacto, só uma atividade de verdade: no repouso o indicador
    /// é o relógio, que já mostra a hora ao lado, e destacá-lo seria ruído.
    /// </summary>
    public static string? FocusedModule(IslandState state, string? indicatorModuleId, string? focusedModuleId) =>
        state != IslandState.Compact ? focusedModuleId : indicatorModuleId == IdleModuleId ? null : indicatorModuleId;

    private static DockBadge? BadgeFor(IReadOnlyDictionary<string, DockBadge>? badges, string moduleId) =>
        badges is not null && badges.TryGetValue(moduleId, out var badge) ? badge : null;

    /// <summary>O módulo "app" (configurações, atualização) não é um módulo da dock.</summary>
    private static PageIcon? Describe(string moduleId) =>
        moduleId == PageIcons.AppModuleId ? null : PageIcons.For(moduleId);

    private static int OrderOf(string moduleId)
    {
        var index = Array.IndexOf(ModuleOrder, moduleId);
        return index < 0 ? ModuleOrder.Length : index;
    }
}
