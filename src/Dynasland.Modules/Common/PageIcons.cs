namespace Dynasland.Modules.Common;

/// <summary>Ícone e nome de uma aba do estado interativo.</summary>
/// <param name="Glyph">Glifo de fonte ou chave "vector:..." (ver <see cref="VectorIcons"/>).</param>
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
        ["media"] = new(Glyphs.MusicNote, "Mídia"),
        ["volume"] = new(Glyphs.VolumeHigh, "Volume"),
        ["brightness"] = new(Glyphs.Brightness, "Brilho"),
        ["timer"] = new(Glyphs.Timer, "Temporizador"),
        ["weather"] = new(Glyphs.Weather, "Clima"),
        ["calendar"] = new(Glyphs.Calendar, "Agenda"),
        ["downloads"] = new(Glyphs.Download, "Downloads"),
        ["bluetooth"] = new(Glyphs.Bluetooth, "Bluetooth"),
        ["battery"] = new(Glyphs.Battery, "Bateria"),
        ["capture"] = new(Glyphs.Camera, "Captura"),
        ["clipboard"] = new(Glyphs.Clipboard, "Área de transferência"),
        ["notifications"] = new(Glyphs.Notification, "Notificações"),
        ["system"] = new(Glyphs.Diagnostic, "Sistema"),
        ["gaming"] = new(Glyphs.Game, "Jogo"),
        ["clock"] = new(Glyphs.Clock, "Relógio"),
        ["water"] = new(VectorIcons.Water, "Água"),
        [AppModuleId] = new(Glyphs.Refresh, "Atualização"),
    };

    /// <summary>O ícone da aba do módulo, ou <c>null</c> se o módulo não for conhecido.</summary>
    public static PageIcon? For(string? moduleId) =>
        moduleId is not null && Icons.TryGetValue(moduleId, out var icon) ? icon : null;
}
