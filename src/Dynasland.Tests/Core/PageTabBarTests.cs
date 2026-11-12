using Dynasland.App.ViewModels;
using Dynasland.Core.Activities;
using Dynasland.Core.Models;
using Dynasland.Modules.Common;

namespace Dynasland.Tests.Core;

public class PageTabBarTests
{
    private static IslandSnapshot Interactive(int pageIndex, params string[] modules) =>
        IslandSnapshot.Empty with
        {
            State = IslandState.Interactive,
            PageCount = modules.Length,
            PageIndex = pageIndex,
            PageModules = new PageModuleList(modules),
        };

    [Fact]
    public void Builds_one_tab_per_page_with_icon_name_and_current_flag()
    {
        var pages = PageTabBar.PagesFor(Interactive(1, "media", "water", "timer"));

        Assert.Equal([0, 1, 2], pages.Select(p => p.Index));
        Assert.Equal([false, true, false], pages.Select(p => p.IsCurrent));
        Assert.Equal(VectorIcons.Music, pages[0].Glyph);
        Assert.Equal(VectorIcons.Water, pages[1].Glyph);
        Assert.True(pages.All(p => p.HasGlyph));
        Assert.Equal("Ir para Água", pages[1].AccessibleName);
    }

    [Fact]
    public void Unknown_module_gets_no_glyph_and_the_numbered_name()
    {
        var pages = PageTabBar.PagesFor(Interactive(0, "media", "unknown"));

        Assert.False(pages[1].HasGlyph);
        Assert.Null(pages[1].Glyph);
        Assert.Equal("Ir para a página 2", pages[1].AccessibleName);
    }

    [Fact]
    public void Single_page_or_outside_interactive_has_no_tabs()
    {
        Assert.Empty(PageTabBar.PagesFor(Interactive(0, "media")));
        Assert.Empty(PageTabBar.PagesFor(Interactive(0, "media", "timer") with { State = IslandState.Expanded }));
    }

    [Fact]
    public void Snapshot_without_page_modules_still_gets_plain_dots()
    {
        var pages = PageTabBar.PagesFor(IslandSnapshot.Empty with { State = IslandState.Interactive, PageCount = 3, PageIndex = 2 });

        Assert.Equal(3, pages.Count);
        Assert.True(pages[2].IsCurrent);
        Assert.DoesNotContain(pages, p => p.HasGlyph);
    }

    [Fact]
    public void Shows_icons_only_when_every_page_has_one()
    {
        Assert.True(PageTabBar.ShowsIcons(PageTabBar.PagesFor(Interactive(0, "media", "timer", "water")), 400));
        Assert.False(PageTabBar.ShowsIcons(PageTabBar.PagesFor(Interactive(0, "media", "unknown")), 400));
        Assert.False(PageTabBar.ShowsIcons([], 400));
    }

    [Theory]
    [InlineData(7, 400, true)]
    [InlineData(8, 420, false)]
    [InlineData(6, 360, true)]
    [InlineData(7, 360, false)]
    public void Falls_back_to_dots_when_the_tabs_do_not_fit_the_footer(int count, double islandWidth, bool expected)
    {
        var modules = Enumerable.Repeat("media", count).ToArray();

        Assert.Equal(expected, PageTabBar.ShowsIcons(PageTabBar.PagesFor(Interactive(0, modules)), islandWidth));
    }
}
