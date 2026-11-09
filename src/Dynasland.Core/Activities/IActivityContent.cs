using Dynasland.Core.Layout;
using Dynasland.Core.Models;

namespace Dynasland.Core.Activities;

/// <summary>
/// Conteúdo (view model) de uma atividade. A interface o apresenta com o DataTemplate
/// registrado com a chave <c>"{NomeCompletoDoTipo}:{Estado}"</c> (ex.: <c>...ClockContent:Interactive</c>).
/// </summary>
public interface IActivityContent
{
    /// <summary>Tamanho desejado da ilha neste estado; <c>null</c> usa o padrão de <see cref="IslandLayout"/>.</summary>
    IslandDimensions? GetPreferredDimensions(IslandState state) => null;
}
