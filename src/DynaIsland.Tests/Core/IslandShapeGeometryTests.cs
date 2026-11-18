using System.Windows;
using DynaIsland.App.Views;

namespace DynaIsland.Tests.Core;

public class IslandShapeGeometryTests
{
    [Fact]
    public void Float_shape_is_a_rounded_rectangle_of_the_given_size()
    {
        var shape = IslandShapeGeometry.Create(250, 32, 16, notch: false);

        Assert.Equal(new Rect(0, 0, 250, 32), shape.Bounds);
        Assert.False(shape.FillContains(new Point(0.5, 0.5)), "canto de cima arredondado");
    }

    [Fact]
    public void Notch_shape_keeps_the_top_corners_square()
    {
        var shape = IslandShapeGeometry.Create(250, 32, 16, notch: true);

        Assert.Equal(new Rect(0, 0, 250, 32), shape.Bounds);
        Assert.True(shape.FillContains(new Point(0.5, 0.5)), "canto de cima reto");
        Assert.False(shape.FillContains(new Point(0.5, 31.5)), "canto de baixo arredondado");
    }

    [Fact]
    public void Shadow_clip_leaves_a_hole_where_the_capsule_is()
    {
        var clip = IslandShapeGeometry.ShadowClip(250, 32, 16, notch: false);

        Assert.False(clip.FillContains(new Point(125, 16)), "dentro da cápsula: sem sombra");
        Assert.True(clip.FillContains(new Point(125, 50)), "abaixo da cápsula: sombra");
    }
}
