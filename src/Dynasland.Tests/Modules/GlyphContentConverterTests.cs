using System.Globalization;
using Dynasland.Modules.Common;

namespace Dynasland.Tests.Modules;

public class GlyphContentConverterTests
{
    private readonly GlyphContentConverter _converter = new();

    private object? Convert(object? value, string? parameter = null) =>
        _converter.Convert(value!, typeof(object), parameter!, CultureInfo.InvariantCulture);

    [Fact]
    public void Font_glyph_passes_through_untouched() => Assert.Equal(Glyphs.Play, Convert(Glyphs.Play));

    [Fact]
    public void Known_vector_key_becomes_its_vector_icon()
    {
        var icon = Assert.IsType<VectorIcon>(Convert(VectorIcons.Water));
        Assert.Same(VectorIcons.Get(VectorIcons.Water), icon);
        Assert.False(string.IsNullOrWhiteSpace(icon.Outline));
    }

    [Fact]
    public void Unknown_vector_key_passes_through_untouched() => Assert.Equal("vector:unknown", Convert("vector:unknown"));

    [Fact]
    public void Glyph_part_hides_vector_keys_and_keeps_font_glyphs()
    {
        Assert.Equal(string.Empty, Convert(VectorIcons.Water, GlyphContentConverter.GlyphPart));
        Assert.Equal(Glyphs.Play, Convert(Glyphs.Play, GlyphContentConverter.GlyphPart));
    }

    [Fact]
    public void Vector_part_is_empty_for_font_glyphs()
    {
        Assert.Null(Convert(Glyphs.Play, GlyphContentConverter.VectorPart));
        Assert.IsType<VectorIcon>(Convert(VectorIcons.Water, GlyphContentConverter.VectorPart));
    }
}
