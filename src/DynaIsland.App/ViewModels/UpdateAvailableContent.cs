using CommunityToolkit.Mvvm.Input;
using DynaIsland.App.Services;
using DynaIsland.Core.Activities;
using DynaIsland.Core.Layout;
using DynaIsland.Core.Models;
using DynaIsland.Modules.Common;

namespace DynaIsland.App.ViewModels;

/// <summary>Atualização na ilha: aviso de nova versão, progresso do download e instalação.</summary>
public sealed partial class UpdateAvailableContent(UpdateFlow flow, Action dismiss) : IActivityContent
{
    public UpdateFlow Flow { get; } = flow;

    public string Glyph => Glyphs.Download;

    public IslandDimensions? GetPreferredDimensions(IslandState state) => state switch
    {
        IslandState.Expanded => new IslandDimensions(340, 64, 30),
        IslandState.Interactive => new IslandDimensions(360, 110, 32),
        _ => null,
    };

    [RelayCommand]
    private void Dismiss() => dismiss();
}
