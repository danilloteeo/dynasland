namespace Dynasland.Core.Activities;

public enum ActivityKind
{
    /// <summary>Permanece enquanto a atividade existir (ex.: música tocando, download em andamento).</summary>
    Live,

    /// <summary>Exibida por um período e descartada (ex.: volume alterado, notificação).</summary>
    Transient,
}
