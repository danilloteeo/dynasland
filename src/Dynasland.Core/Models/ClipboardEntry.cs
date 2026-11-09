namespace Dynasland.Core.Models;

public enum ClipboardEntryKind
{
    Text,
    Image,
    Other,
}

/// <summary>Conteúdo da área de transferência (atual ou do histórico do Windows).</summary>
/// <param name="Id">Identificador do item no histórico do Windows; vazio para o conteúdo atual.</param>
/// <param name="Image">Bytes da imagem (PNG/BMP) quando <see cref="Kind"/> é <see cref="ClipboardEntryKind.Image"/>.</param>
public sealed record ClipboardEntry(string Id, ClipboardEntryKind Kind, string? Text, byte[]? Image, DateTimeOffset Timestamp);
