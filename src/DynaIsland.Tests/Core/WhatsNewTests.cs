using DynaIsland.Core.Models;
using DynaIsland.Core.Services;

namespace DynaIsland.Tests.Core;

public class WhatsNewTests
{
    [Fact]
    public void Shows_once_for_a_version_that_has_notes()
    {
        Assert.True(WhatsNew.ShouldShow(null, "1.6.0"));
        Assert.True(WhatsNew.ShouldShow("1.5.0", "1.6.0"));
        Assert.False(WhatsNew.ShouldShow("1.6.0", "1.6.0"));
    }

    [Fact]
    public void Does_not_show_for_a_version_without_notes()
    {
        Assert.False(WhatsNew.ShouldShow("1.6.0", "9.9.9"));
        Assert.Empty(WhatsNew.For("9.9.9"));
    }

    [Fact]
    public void Every_page_has_a_title_and_text()
    {
        var pages = WhatsNew.For("1.6.0");

        Assert.NotEmpty(pages);
        Assert.All(pages, p => Assert.False(string.IsNullOrWhiteSpace(p.Title) || string.IsNullOrWhiteSpace(p.Text)));
    }

    [Theory]
    [InlineData("1.6.0")]
    [InlineData("1.7.0")]
    [InlineData("1.8.1")]
    public void Every_published_note_page_is_complete(string version)
    {
        var pages = WhatsNew.For(version);

        Assert.NotEmpty(pages);
        Assert.All(pages, p => Assert.False(string.IsNullOrWhiteSpace(p.Title) || string.IsNullOrWhiteSpace(p.Text)));
    }

    [Fact]
    public void Renamed_release_is_shown_to_users_coming_from_the_old_name()
    {
        Assert.True(WhatsNew.ShouldShow("1.7.0", "1.8.1"));
        Assert.True(WhatsNew.ShouldShow("1.7.1", "1.8.1"));
        Assert.True(WhatsNew.ShouldShow(null, "1.8.1"));
        Assert.False(WhatsNew.ShouldShow("1.8.1", "1.8.1"));
        Assert.Empty(WhatsNew.For("1.8.0")); // saiu sem páginas: nada a mostrar nela
    }

    [Fact]
    public void Last_seen_version_round_trips_through_the_settings_mapper()
    {
        var values = IslandSettingsMapper.ToValues(IslandSettings.Default with { LastSeenVersion = "1.6.0" });

        Assert.Equal("1.6.0", IslandSettingsMapper.FromValues(values).LastSeenVersion);
        Assert.Null(IslandSettingsMapper.FromValues(new Dictionary<string, string>()).LastSeenVersion);
    }
}
