#nullable enable
namespace SoulMaskServerManager.Helpers;

/// <summary>
/// TabControl that overrides WndProc to repaint the tab strip background AND
/// content-area border after Windows draws them (light), replacing with dark colors.
/// Uses DisplayRectangle to precisely cover only the Windows-drawn border pixels.
/// </summary>
public sealed class DarkTabControl : TabControl
{
    private static Color StripBg    => Color.FromArgb(22, 22, 22);
    private static Color SelectedBg => Color.FromArgb(37, 37, 38);
    private static Color NormalBg   => Color.FromArgb(22, 22, 22);
    private static Color BorderBg   => Color.FromArgb(37, 37, 38);
    private static Color Fg         => Color.FromArgb(204, 204, 204);
    private static Color AccentLine => Color.FromArgb(0, 122, 204);

    public DarkTabControl()
    {
        DrawMode = TabDrawMode.OwnerDrawFixed;
        SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
    }

    protected override void OnDrawItem(DrawItemEventArgs e)
    {
        if (e.Index < 0 || e.Index >= TabPages.Count) return;
        bool selected = e.Index == SelectedIndex;
        Color bg = selected ? SelectedBg : NormalBg;

        using var bgBrush = new SolidBrush(bg);
        e.Graphics.FillRectangle(bgBrush, e.Bounds);

        if (selected)
        {
            using var pen = new Pen(AccentLine, 2);
            e.Graphics.DrawLine(pen, e.Bounds.Left, e.Bounds.Top,
                                     e.Bounds.Right, e.Bounds.Top);
        }

        string text = TabPages[e.Index].Text;
        TextRenderer.DrawText(e.Graphics, text, Font, e.Bounds, Fg,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
            TextFormatFlags.EndEllipsis);
    }

    protected override void WndProc(ref Message m)
    {
        // Intercept WM_ERASEBKGND so Windows can't paint a light background
        if (m.Msg == 0x0014 /* WM_ERASEBKGND */)
        {
            using var g = Graphics.FromHdc(m.WParam);
            g.Clear(StripBg);
            m.Result = (IntPtr)1;
            return;
        }

        base.WndProc(ref m);

        // After Windows finishes painting, repaint strip + border
        if (m.Msg == 0x000F /* WM_PAINT */ && TabPages.Count > 0)
        {
            using var g = Graphics.FromHwnd(Handle);
            RepaintStrip(g);
            RepaintBorder(g);
        }
    }

    private void RepaintStrip(Graphics g)
    {
        if (TabPages.Count == 0) return;
        var first = GetTabRect(0);

        using var br = new SolidBrush(StripBg);
        g.FillRectangle(br, 0, 0, Width, first.Bottom + 2);

        for (int i = 0; i < TabPages.Count; i++)
        {
            var r = GetTabRect(i);
            OnDrawItem(new DrawItemEventArgs(g, Font, r, i,
                i == SelectedIndex ? DrawItemState.Selected : DrawItemState.Default));
        }
    }

    /// <summary>
    /// Fill the border region around the content area (everything between the
    /// strip and DisplayRectangle, plus left/right/bottom margins).
    /// DisplayRectangle is the exact content rect, so this covers the Windows
    /// 3-D border precisely with no gaps or over-painting.
    /// </summary>
    private void RepaintBorder(Graphics g)
    {
        if (TabPages.Count == 0) return;
        var d = DisplayRectangle; // exact content area
        using var br = new SolidBrush(BorderBg);

        // Left strip between client edge and content
        if (d.Left > 0)
            g.FillRectangle(br, 0, d.Top, d.Left, d.Height);
        // Right strip
        if (d.Right < Width)
            g.FillRectangle(br, d.Right, d.Top, Width - d.Right, d.Height);
        // Bottom strip
        if (d.Bottom < Height)
            g.FillRectangle(br, 0, d.Bottom, Width, Height - d.Bottom);
        // Gap between tab strip bottom and content top
        var first = GetTabRect(0);
        int stripBottom = first.Bottom + 2;
        if (d.Top > stripBottom)
            g.FillRectangle(br, 0, stripBottom, Width, d.Top - stripBottom);
    }
}
