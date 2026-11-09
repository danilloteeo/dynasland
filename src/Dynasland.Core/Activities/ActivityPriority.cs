namespace Dynasland.Core.Activities;

public enum ActivityPriority
{
    Low = 0,
    Normal = 1,

    /// <summary>Pode interromper a ilha enquanto o usuário a está usando.</summary>
    High = 2,
    Critical = 3,
}
