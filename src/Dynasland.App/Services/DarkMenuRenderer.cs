using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Dynasland.App.Services;

/// <summary>Renderizador escuro para o menu de contexto da bandeja.</summary>
internal sealed class DarkMenuRenderer() : ToolStripProfessionalRenderer(new DarkColorTable())
{
    private static readonly Color TextColor = Color.FromArgb(242, 242, 242);
    private static readonly Color DisabledTextColor = Color.FromArgb(120, 120, 120);
    private static readonly Color AccentColor = Color.FromArgb(76, 194, 255);

    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {
        e.TextColor = e.Item.Enabled ? TextColor : DisabledTextColor;
        base.OnRenderItemText(e);
    }

    protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
    {
        e.ArrowColor = TextColor;
        base.OnRenderArrow(e);
    }

    protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
    {
        var bounds = e.ImageRectangle;
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using var pen = new Pen(AccentColor, 2f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
        g.DrawLines(pen,
        [
            new PointF(bounds.Left + bounds.Width * 0.22f, bounds.Top + bounds.Height * 0.52f),
            new PointF(bounds.Left + bounds.Width * 0.42f, bounds.Top + bounds.Height * 0.72f),
            new PointF(bounds.Left + bounds.Width * 0.78f, bounds.Top + bounds.Height * 0.30f),
        ]);
    }

    private sealed class DarkColorTable : ProfessionalColorTable
    {
        private static readonly Color Background = Color.FromArgb(32, 32, 32);
        private static readonly Color Hover = Color.FromArgb(55, 55, 55);
        private static readonly Color Border = Color.FromArgb(60, 60, 60);

        public override Color ToolStripDropDownBackground => Background;
        public override Color ImageMarginGradientBegin => Background;
        public override Color ImageMarginGradientMiddle => Background;
        public override Color ImageMarginGradientEnd => Background;
        public override Color MenuBorder => Border;
        public override Color MenuItemBorder => Hover;
        public override Color MenuItemSelected => Hover;
        public override Color MenuItemSelectedGradientBegin => Hover;
        public override Color MenuItemSelectedGradientEnd => Hover;
        public override Color MenuItemPressedGradientBegin => Hover;
        public override Color MenuItemPressedGradientEnd => Hover;
        public override Color SeparatorDark => Border;
        public override Color SeparatorLight => Background;
        public override Color CheckBackground => Background;
        public override Color CheckSelectedBackground => Hover;
        public override Color CheckPressedBackground => Hover;
    }
}
