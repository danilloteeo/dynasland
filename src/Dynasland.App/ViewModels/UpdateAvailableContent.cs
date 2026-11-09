using CommunityToolkit.Mvvm.Input;
using Dynasland.Core.Activities;
using Dynasland.Core.Layout;
using Dynasland.Core.Models;

namespace Dynasland.App.ViewModels;

/// <summary>Aviso de nova versão exibido na ilha.</summary>
public sealed partial class UpdateAvailableContent(string version, Action download, Action dismiss) : IActivityContent
{
    public string Version { get; } = version;

    public string Glyph => "";

    public IslandDimensions? GetPreferredDimensions(IslandState state) => state switch
    {
        IslandState.Expanded => new IslandDimensions(340, 64, 30),
        IslandState.Interactive => new IslandDimensions(360, 110, 32),
        _ => null,
    };

    [RelayCommand]
    private void Download() => download();

    [RelayCommand]
    private void Dismiss() => dismiss();
}
