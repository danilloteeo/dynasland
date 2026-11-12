namespace Dynasland.Modules.Common;

/// <summary>Ícone e nome de uma aba do estado interativo.</summary>
/// <param name="Glyph">Chave "vector:..." (ver <see cref="VectorIcons"/>).</param>
/// <param name="Name">Nome curto da aba, para leitores de tela.</param>
public sealed record PageIcon(string Glyph, string Name);

/// <summary>
/// Ícone de cada aba pelo módulo da atividade (barra de abas do rodapé, como a do Volum).
/// Módulo desconhecido devolve <c>null</c>: a barra volta aos pontos.
/// </summary>
public static class PageIcons
{
    /// <summary>Atividades do próprio aplicativo (ex.: atualização).</summary>
    public const string AppModuleId = "app";

    private static readonly Dictionary<string, PageIcon> Icons = new(StringComparer.Ordinal)
    {
        ["media"] = new(VectorIcons.Music, "Mídia"),
        ["volume"] = new(VectorIcons.Volume, "Volume"),
        ["brightness"] = new(VectorIcons.Brightness, "Brilho"),
        ["timer"] = new(VectorIcons.Timer, "Temporizador"),
        ["weather"] = new(VectorIcons.Weather, "Clima"),
        ["calendar"] = new(VectorIcons.Calendar, "Agenda"),
        ["downloads"] = new(VectorIcons.Download, "Downloads"),
        ["bluetooth"] = new(VectorIcons.Bluetooth, "Bluetooth"),
        ["battery"] = new(VectorIcons.Battery, "Bateria"),
        ["capture"] = new(VectorIcons.Camera, "Captura"),
        ["clipboard"] = new(VectorIcons.Clipboard, "Área de transferência"),
        ["notifications"] = new(VectorIcons.Bell, "Notificações"),
        ["system"] = new(VectorIcons.Pulse, "Sistema"),
        ["gaming"] = new(VectorIcons.Game, "Jogo"),
        ["clock"] = new(VectorIcons.Clock, "Relógio"),
        ["water"] = new(VectorIcons.Water, "Água"),
        [AppModuleId] = new(VectorIcons.Refresh, "Atualização"),
    };

    /// <summary>O ícone da aba do módulo, ou <c>null</c> se o módulo não for conhecido.</summary>
    public static PageIcon? For(string? moduleId) =>
        moduleId is not null && Icons.TryGetValue(moduleId, out var icon) ? icon : null;
}
