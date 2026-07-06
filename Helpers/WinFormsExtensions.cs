namespace SoulMaskServerManager.Helpers;

public static class WinFormsExtensions
{
    /// <summary>Invoke on UI thread if required, otherwise call directly.</summary>
    public static void InvokeIfRequired(this Control control, Action action)
    {
        if (control.IsDisposed || control.Disposing) return;
        if (control.InvokeRequired)
        {
            try { control.Invoke(action); }
            catch (ObjectDisposedException) { }
        }
        else
        {
            action();
        }
    }

    /// <summary>Append a timestamped line to the console (works for TextBox and RichTextBox).</summary>
    public static void AppendConsoleLine(this TextBoxBase tb, string message,
        Color? color = null, bool autoScroll = true)
    {
        string timestamp = DateTime.Now.ToString("HH:mm:ss");
        tb.AppendText($"[{timestamp}] {message}{Environment.NewLine}");

        // Keep the buffer from growing unbounded. Check TextLength (cheap) each
        // call — materializing tb.Lines per append is O(buffer) and made long
        // installs progressively slower.
        const int maxChars  = 600_000;
        const int keepChars = 300_000;
        if (tb.TextLength > maxChars)
        {
            string text = tb.Text;
            // Cut on a line boundary just past the overflow point
            int cut = text.IndexOf('\n', text.Length - keepChars);
            if (cut >= 0)
            {
                bool wasReadOnly = tb.ReadOnly;
                tb.ReadOnly = false;
                tb.Select(0, cut + 1);
                tb.SelectedText = "";
                tb.ReadOnly = wasReadOnly;
            }
        }

        if (autoScroll && tb.IsHandleCreated)
        {
            // SB_BOTTOM scrolls to the absolute end. When content fits in the box
            // the scroll range is 0, so this lands at position 0 (top) — no clipping.
            // When content overflows it scrolls to the last line.
            NativeMethods.SendMessage(tb.Handle, NativeMethods.WM_VSCROLL, NativeMethods.SB_BOTTOM, 0);
        }
    }

    /// <summary>Classify a console output line (kept for callers that pass color — ignored now).</summary>
    public static Color ClassifyConsoleLine(string line)
    {
        // Color classification is kept for API compatibility but not used with TextBox.
        return Color.FromArgb(204, 204, 204);
    }
}
