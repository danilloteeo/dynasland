using Dynasland.Core.Events;

namespace Dynasland.Core.Activities;

public sealed record ActivityPostedEvent(IslandActivity Activity);

public sealed record ActivityRemovedEvent(string ActivityId);

/// <summary>Publicado quando um módulo é parado: suas atividades devem sair da ilha.</summary>
public sealed record ModuleStoppedEvent(string ModuleId);

public static class ActivityEventBusExtensions
{
    public static void PostActivity(this IEventBus bus, IslandActivity activity) =>
        bus.Publish(new ActivityPostedEvent(activity));

    public static void RemoveActivity(this IEventBus bus, string activityId) =>
        bus.Publish(new ActivityRemovedEvent(activityId));
}
