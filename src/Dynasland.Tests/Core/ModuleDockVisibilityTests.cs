using Dynasland.App.ViewModels;
using Dynasland.Core.Modules;

namespace Dynasland.Tests.Core;

public class ModuleDockVisibilityTests
{
    private static ModuleSettingViewModel Create(string id, bool running = true, bool showInDock = true, Func<string, bool, Task>? setDockVisible = null) =>
        new(
            new ModuleInfo(id, id, "descrição", IsEnabled: true, IsRunning: running, Error: null),
            [],
            (_, _) => Task.CompletedTask,
            setDockVisible ?? ((_, _) => Task.CompletedTask),
            showInDock);

    [Fact]
    public void Modules_with_a_dock_icon_offer_the_option_only_while_running()
    {
        Assert.True(Create("volume").ShowDockRow);
        Assert.False(Create("volume", running: false).ShowDockRow);
    }

    [Theory]
    [InlineData("desconhecido")]
    [InlineData("app")]
    public void Modules_without_a_dock_icon_do_not_offer_the_option(string id)
    {
        Assert.False(Create(id).CanShowInDock);
    }

    [Fact]
    public void Switching_the_option_reports_the_module_and_the_new_value()
    {
        var calls = new List<(string Id, bool Visible)>();
        var module = Create("weather", setDockVisible: (id, visible) =>
        {
            calls.Add((id, visible));
            return Task.CompletedTask;
        });
        Assert.True(module.ShowInDock);

        module.ShowInDock = false;

        Assert.Equal([("weather", false)], calls);
    }
}
