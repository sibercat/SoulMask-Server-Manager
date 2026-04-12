#nullable enable
using SoulMaskServerManager.Helpers;
namespace SoulMaskServerManager.Forms;

partial class ClusterManagerForm
{
    private System.ComponentModel.IContainer? components = null;

    protected MenuStrip menuStrip;
    protected ToolStripMenuItem menuFile, menuHelp, menuHelpAbout;
    protected ToolStripMenuItem menuAddInstance, menuRenameInstance, menuRemoveInstance, menuExit;
    protected DarkTabControl tcInstances;
    protected StatusStrip statusStrip;

    protected override void Dispose(bool disposing)
    {
        if (disposing && components != null) components.Dispose();
        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        components = new System.ComponentModel.Container();
        SuspendLayout();

        Text          = $"SoulMask Server Manager  v{Application.ProductVersion}";
        Size          = new Size(1280, 820);
        MinimumSize   = new Size(1100, 700);
        StartPosition = FormStartPosition.CenterScreen;
        Font          = new Font("Segoe UI", 9f, FontStyle.Regular, GraphicsUnit.Point);
        Icon          = Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? SystemIcons.Application;

        // ── Menu ─────────────────────────────────────────────────────
        menuStrip = new MenuStrip();

        menuFile   = new ToolStripMenuItem("File");
        menuHelp   = new ToolStripMenuItem("Help");

        menuAddInstance    = new ToolStripMenuItem("Add Server Instance…");
        menuRenameInstance = new ToolStripMenuItem("Rename Current Instance…");
        menuRemoveInstance = new ToolStripMenuItem("Remove Current Instance…");
        menuExit           = new ToolStripMenuItem("Exit");

        menuFile.DropDownItems.AddRange([
            menuAddInstance,
            menuRenameInstance,
            menuRemoveInstance,
            new ToolStripSeparator(),
            menuExit
        ]);

        menuHelpAbout = new ToolStripMenuItem("About");
        menuHelp.DropDownItems.Add(menuHelpAbout);

        menuStrip.Items.AddRange([menuFile, menuHelp]);

        // ── Tab control — one tab per server instance ─────────────────
        tcInstances = new DarkTabControl
        {
            Dock     = DockStyle.Fill,
            Font     = new Font("Segoe UI", 10f, FontStyle.Bold),
            Padding  = new Point(18, 6),
            ItemSize = new Size(180, 32)
        };

        // ── Status strip — shows all instances at a glance ───────────
        statusStrip = new StatusStrip { SizingGrip = false };

        Controls.Add(tcInstances);
        Controls.Add(statusStrip);
        Controls.Add(menuStrip);
        MainMenuStrip = menuStrip;

        ResumeLayout(false);
        PerformLayout();
    }
}
