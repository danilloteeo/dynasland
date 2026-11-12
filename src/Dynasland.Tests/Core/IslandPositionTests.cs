using System.Globalization;
using System.Windows;
using Dynasland.App.Converters;
using Dynasland.Core.Layout;
using Dynasland.Core.Models;
using Dynasland.Core.Services;

namespace Dynasland.Tests.Core;

public class IslandPositionTests
{
    private static readonly PixelRect FullHd = new(0, 0, 1920, 1040);

    [Fact]
    public void Defaults_keep_the_floating_island_centered()
    {
        Assert.Equal(IslandPosition.Center, IslandSettings.Default.Position);
        Assert.Equal(IslandShape.Float, IslandSettings.Default.Shape);
    }

    [Fact]
    public void Position_and_shape_round_trip_through_storage()
    {
        var custom = IslandSettings.Default with { Position = IslandPosition.Right, Shape = IslandShape.Notch };

        var restored = IslandSettingsMapper.FromValues(IslandSettingsMapper.ToValues(custom));

        Assert.Equal(IslandPosition.Right, restored.Position);
        Assert.Equal(IslandShape.Notch, restored.Shape);
    }

    [Theory]
    [InlineData("Topo", "Quadrada")]
    [InlineData("", "")]
    [InlineData("99", "7")]
    public void Unknown_position_and_shape_fall_back_to_defaults(string position, string shape)
    {
        var values = new Dictionary<string, string>
        {
            [IslandSettingsMapper.Keys.Position] = position,
            [IslandSettingsMapper.Keys.Shape] = shape,
        };

        var settings = IslandSettingsMapper.FromValues(values);

        Assert.Equal(IslandPosition.Center, settings.Position);
        Assert.Equal(IslandShape.Float, settings.Shape);
    }

    [Fact]
    public void Missing_position_and_shape_use_defaults()
    {
        var settings = IslandSettingsMapper.FromValues(new Dictionary<string, string>());

        Assert.Equal(IslandPosition.Center, settings.Position);
        Assert.Equal(IslandShape.Float, settings.Shape);
    }

    [Fact]
    public void Center_window_uses_the_centered_formula()
    {
        var point = IslandPlacementCalculator.Place(FullHd, windowWidth: 480, IslandPosition.Center, topMargin: 8);

        Assert.Equal(IslandPlacementCalculator.TopCenter(FullHd, 480, 8), point);
        Assert.Equal(new PixelPoint(720, 8), point);
    }

    [Fact]
    public void Left_window_starts_at_the_work_area_left_edge()
    {
        var workArea = new PixelRect(-2560, 0, 0, 1400);

        var point = IslandPlacementCalculator.Place(workArea, windowWidth: 720, IslandPosition.Left, topMargin: 12);

        Assert.Equal(new PixelPoint(-2560, 12), point);
    }

    [Fact]
    public void Right_window_ends_at_the_work_area_right_edge()
    {
        var workArea = new PixelRect(1920, 48, 4480, 1440);

        var point = IslandPlacementCalculator.Place(workArea, windowWidth: 720, IslandPosition.Right, topMargin: 0);

        Assert.Equal(new PixelPoint(3760, 48), point);
    }

    [Theory]
    [InlineData(IslandShape.Notch, 8, 0)]
    [InlineData(IslandShape.Notch, 40, 0)]
    [InlineData(IslandShape.Float, 8, 8)]
    [InlineData(IslandShape.Float, 24, 24)]
    public void Notch_ignores_the_top_margin(IslandShape shape, double topMargin, double expected)
    {
        Assert.Equal(expected, IslandPlacementCalculator.Top(shape, topMargin));
    }

    [Fact]
    public void Notch_window_sits_on_the_top_of_the_work_area()
    {
        var workArea = new PixelRect(0, 48, 1920, 1080);

        var point = IslandPlacementCalculator.Place(workArea, 480, IslandPosition.Center, IslandPlacementCalculator.Top(IslandShape.Notch, 8));

        Assert.Equal(48, point.Y);
    }

    [Theory]
    [InlineData(190)]
    [InlineData(300)]
    [InlineData(420)]
    public void Left_island_keeps_its_left_edge_while_the_width_changes(double islandWidth)
    {
        var left = IslandPlacementCalculator.IslandLeft(IslandPosition.Left, windowWidth: 480, islandWidth, IslandPlacementCalculator.EdgeMargin);

        Assert.Equal(16, left);
    }

    [Theory]
    [InlineData(190)]
    [InlineData(300)]
    [InlineData(420)]
    public void Right_island_keeps_its_right_edge_while_the_width_changes(double islandWidth)
    {
        var left = IslandPlacementCalculator.IslandLeft(IslandPosition.Right, windowWidth: 480, islandWidth, IslandPlacementCalculator.EdgeMargin);

        Assert.Equal(480 - 16, left + islandWidth);
    }

    [Theory]
    [InlineData(190)]
    [InlineData(420)]
    public void Center_island_keeps_its_middle_while_the_width_changes(double islandWidth)
    {
        var left = IslandPlacementCalculator.IslandLeft(IslandPosition.Center, windowWidth: 480, islandWidth, IslandPlacementCalculator.EdgeMargin);

        Assert.Equal(240, left + islandWidth / 2);
    }

    [Fact]
    public void Island_on_the_left_is_the_edge_margin_away_from_the_screen_edge()
    {
        // Janela de 480 DIP a 150%: 720 px; a ilha fica a 16 DIP (24 px) da borda esquerda da área útil.
        var workArea = new PixelRect(0, 0, 2560, 1400);
        const double scale = 1.5;
        var window = IslandPlacementCalculator.Place(workArea, 720, IslandPosition.Left, 0);

        var islandScreenLeft = window.X + IslandPlacementCalculator.IslandLeft(IslandPosition.Left, 480, 190, IslandPlacementCalculator.EdgeMargin) * scale;

        Assert.Equal(24, islandScreenLeft);
    }

    [Fact]
    public void Island_on_the_right_is_the_edge_margin_away_from_the_screen_edge()
    {
        var workArea = new PixelRect(0, 0, 2560, 1400);
        const double scale = 1.5;
        var window = IslandPlacementCalculator.Place(workArea, 720, IslandPosition.Right, 0);

        var islandScreenRight = window.X + (IslandPlacementCalculator.IslandLeft(IslandPosition.Right, 480, 300, IslandPlacementCalculator.EdgeMargin) + 300) * scale;

        Assert.Equal(2560 - 24, islandScreenRight);
    }

    [Fact]
    public void Float_shape_rounds_every_corner()
    {
        var radius = Convert(17, IslandShape.Float);

        Assert.Equal(new CornerRadius(17), radius);
    }

    [Fact]
    public void Notch_shape_flattens_only_the_top_corners()
    {
        var radius = Convert(17, IslandShape.Notch);

        Assert.Equal(new CornerRadius(0, 0, 17, 17), radius);
    }

    [Fact]
    public void Corner_converter_tolerates_unset_values()
    {
        var radius = new IslandCornerRadiusConverter().Convert([DependencyProperty.UnsetValue, DependencyProperty.UnsetValue], typeof(CornerRadius), null!, CultureInfo.InvariantCulture);

        Assert.Equal(new CornerRadius(0), radius);
    }

    private static object Convert(double radius, IslandShape shape) =>
        new IslandCornerRadiusConverter().Convert([radius, shape], typeof(CornerRadius), null!, CultureInfo.InvariantCulture);
}
