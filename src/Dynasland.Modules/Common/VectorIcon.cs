namespace Dynasland.Modules.Common;

/// <summary>
/// Ícone de linha desenhado em grade 24×24 (estilo Segoe Fluent Icons): <see cref="Outline"/> é o contorno
/// e <see cref="Detail"/> um traço interno opcional. Ambos são dados de Path, sem preenchimento, na cor do texto.
/// </summary>
public sealed record VectorIcon(string Outline, string Detail);
