using Dynasland.Modules.Common;

namespace Dynasland.Tests.Modules;

public class GlyphsTests
{
    [Fact]
    public void Every_glyph_constant_is_a_known_vector_icon()
    {
        var constants = typeof(Glyphs).GetFields().Where(f => f.IsLiteral).ToList();

        Assert.NotEmpty(constants);
        foreach (var field in constants)
        {
            Assert.True(VectorIcons.Get((string?)field.GetRawConstantValue()) is not null, $"Glyphs.{field.Name} não tem ícone vetorial.");
        }
    }
}
