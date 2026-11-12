using Dynasland.Core.Activities;
using Dynasland.Core.Layout;
using Dynasland.Core.Models;
using Dynasland.Modules.Common;

namespace Dynasland.Modules.KeyLocks;

/// <summary>Aviso rápido de Caps Lock ou Num Lock: nome da tecla e se ficou ligada ou desligada.</summary>
public sealed class KeyLockContent(string key, bool isOn) : IActivityContent
{
    public string Glyph => VectorIcons.Keyboard;

    public string Key { get; } = key;

    public bool IsOn { get; } = isOn;

    public string StateText => IsOn ? "Ligado" : "Desligado";

    public IslandDimensions? GetPreferredDimensions(IslandState state) =>
        state == IslandState.Expanded ? new IslandDimensions(300, 50, 25) : null;
}
