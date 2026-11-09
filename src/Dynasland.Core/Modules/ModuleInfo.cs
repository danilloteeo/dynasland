namespace Dynasland.Core.Modules;

/// <param name="Error">Mensagem da última falha ao iniciar; <c>null</c> se não houve.</param>
public sealed record ModuleInfo(string Id, string DisplayName, string Description, bool IsEnabled, bool IsRunning, string? Error);
