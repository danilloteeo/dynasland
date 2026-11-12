using CommunityToolkit.Mvvm.Input;
using Dynasland.App.Services;
using Dynasland.Core.Activities;
using Dynasland.Core.Layout;
using Dynasland.Core.Models;
using Dynasland.Modules.Common;

namespace Dynasland.App.ViewModels;

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
