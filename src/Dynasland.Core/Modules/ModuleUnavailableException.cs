namespace Dynasland.Core.Modules;

/// <summary>
/// Lançada por <see cref="IIslandModule.StartAsync"/> quando o recurso não existe neste computador
/// (ex.: sem bateria, brilho não controlável). Não é tratada como falha: o módulo fica indisponível.
/// </summary>
public sealed class ModuleUnavailableException(string message) : Exception(message);
