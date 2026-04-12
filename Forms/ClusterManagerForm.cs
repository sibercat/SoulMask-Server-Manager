#nullable enable
using SoulMaskServerManager.Models;
using SoulMaskServerManager.Services;

namespace SoulMaskServerManager.Forms;

public partial class ClusterManagerForm : Form
{
    // ── Paths ─────────────────────────────────────────────────────────
    private static readonly string InstancesDir =
        Path.Combine(AppContext.BaseDirectory, "SoulMaskServer");

    private static readonly string OrderFile =
        Path.Combine(AppContext.BaseDirectory, "SoulMaskServer", "_order.txt");

    // ── Instance tracking ─────────────────────────────────────────────
    private record InstanceEntry(
        string               Name,
        string               RootDir,
        MainForm             Form,
        TabPage              Tab,
        ToolStripStatusLabel StatusLabel);

    private readonly List<InstanceEntry> _instances = [];
    private int _dragTabIndex = -1;

    // ── Constructor ───────────────────────────────────────────────────
    public ClusterManagerForm()
    {
        InitializeComponent();

        // Suppress all redraws during init — prevents white flash before theme is applied
        NativeMethods.SendMessage(Handle, 0x000B /* WM_SETREDRAW */, 0, 0);

        WireMenuEvents();

        // Load existing instances (or create a default one)
        Directory.CreateDirectory(InstancesDir);
        LoadInstances();

        // Apply dark theme
        ThemeManager.Apply(this, AppTheme.Dark);

        // Re-enable redraws and paint once, fully themed
        NativeMethods.SendMessage(Handle, 0x000B /* WM_SETREDRAW */, 1, 0);
        Refresh();

        // Re-apply after Shown so Windows doesn't override our theming
        this.Shown += (_, _) => ThemeManager.ReapplyConsoleThemeOverrides(this);
    }

    // ── Instance loading ──────────────────────────────────────────────

    private void LoadInstances()
    {
        // Legacy layout: SoulMaskServer\ServerFiles\ exists directly → single instance
        if (Directory.Exists(Path.Combine(InstancesDir, "ServerFiles")))
        {
            AddInstance("Server 1", InstancesDir);
            return;
        }

        // Scan subdirectories for instance folders
        var allDirs = Directory.GetDirectories(InstancesDir)
            .Where(d => !Path.GetFileName(d).StartsWith('.'))
            .ToList();

        List<string> dirs;
        if (File.Exists(OrderFile))
        {
            // Restore saved order; append any new dirs not in the file at the end
            var saved = File.ReadAllLines(OrderFile)
                .Select((n, i) => (n, i))
                .ToDictionary(x => x.n, x => x.i, StringComparer.OrdinalIgnoreCase);

            dirs = allDirs
                .OrderBy(d => saved.TryGetValue(Path.GetFileName(d), out int idx) ? idx : int.MaxValue)
                .ThenBy(d => Directory.GetCreationTime(d))
                .ToList();
        }
        else
        {
            dirs = allDirs.OrderBy(d => Directory.GetCreationTime(d)).ToList();
        }

        if (dirs.Count == 0)
        {
            // No instances yet — create a default one
            string defaultDir = Path.Combine(InstancesDir, "Server 1");
            Directory.CreateDirectory(defaultDir);
            AddInstance("Server 1", defaultDir);
            return;
        }

        foreach (var dir in dirs)
            AddInstance(Path.GetFileName(dir), dir);
    }

    private void AddInstance(string name, string rootDir)
    {
        var tab = new TabPage($"  {name}  ");

        var form = new MainForm(rootDir, name, embedded: true)
        {
            TopLevel        = false,
            FormBorderStyle = FormBorderStyle.None,
            Dock            = DockStyle.Fill
        };

        tab.Controls.Add(form);
        tcInstances.TabPages.Add(tab);
        form.Show();

        // Status strip label for this instance
        var statusLabel = new ToolStripStatusLabel($"{name}: Initializing")
        {
            Spring    = false,
            AutoSize  = true,
            Margin    = new Padding(0, 0, 12, 0)
        };

        statusStrip.Items.Add(statusLabel);

        // Update status label + tab text when server state changes
        form.InstanceStateChanged += (_, state) =>
        {
            this.InvokeIfRequired(() =>
            {
                string dot = state switch
                {
                    ServerState.Running  => "● ",
                    ServerState.Starting => "◌ ",
                    ServerState.Stopping => "◌ ",
                    ServerState.Stopped  => "○ ",
                    ServerState.Crashed  => "✖ ",
                    _                    => "  "
                };
                string stateText = state switch
                {
                    ServerState.Running      => "Running",
                    ServerState.Starting     => "Starting...",
                    ServerState.Stopping     => "Stopping...",
                    ServerState.Stopped      => "Stopped",
                    ServerState.NotInstalled => "Not Installed",
                    ServerState.Installing   => "Installing...",
                    ServerState.Crashed      => "Crashed",
                    _                        => state.ToString()
                };

                statusLabel.Text  = $"{dot}{name}: {stateText}";
                tab.Text          = state == ServerState.Running
                    ? $"  {name}  ●  "
                    : $"  {name}  ";
            });
        };

        _instances.Add(new InstanceEntry(name, rootDir, form, tab, statusLabel));
    }

    // ── Menu wiring ───────────────────────────────────────────────────

    private void WireMenuEvents()
    {
        menuAddInstance.Click    += (_, _) => OnAddInstance();
        menuRenameInstance.Click += (_, _) => OnRenameInstance();
        menuRemoveInstance.Click += (_, _) => OnRemoveInstance();
        menuExit.Click           += (_, _) => Close();
        menuHelpAbout.Click      += (_, _) => ShowAbout();

        tcInstances.MouseDoubleClick += (_, e) =>
        {
            for (int i = 0; i < tcInstances.TabPages.Count; i++)
            {
                if (tcInstances.GetTabRect(i).Contains(e.Location))
                {
                    tcInstances.SelectedIndex = i;
                    OnRenameInstance();
                    break;
                }
            }
        };

        // Drag-to-reorder tabs
        tcInstances.MouseDown += (_, e) =>
        {
            if (e.Button != MouseButtons.Left) return;
            _dragTabIndex = -1;
            for (int i = 0; i < tcInstances.TabPages.Count; i++)
                if (tcInstances.GetTabRect(i).Contains(e.Location))
                { _dragTabIndex = i; break; }
        };

        tcInstances.MouseMove += (_, e) =>
        {
            if (_dragTabIndex < 0 || e.Button != MouseButtons.Left) return;
            for (int i = 0; i < tcInstances.TabPages.Count; i++)
            {
                if (!tcInstances.GetTabRect(i).Contains(e.Location) || i == _dragTabIndex) continue;

                // Swap tab pages
                var dragTab = tcInstances.TabPages[_dragTabIndex];
                tcInstances.TabPages.RemoveAt(_dragTabIndex);
                tcInstances.TabPages.Insert(i, dragTab);

                // Keep _instances list in sync
                var dragInst = _instances[_dragTabIndex];
                _instances.RemoveAt(_dragTabIndex);
                _instances.Insert(i, dragInst);

                // Keep status strip labels in sync with new order
                SyncStatusStripOrder();

                _dragTabIndex = i;
                tcInstances.SelectedIndex = i;
                break;
            }
        };

        tcInstances.MouseUp += (_, e) =>
        {
            if (_dragTabIndex >= 0 && e.Button == MouseButtons.Left)
            {
                SaveInstanceOrder();
                _dragTabIndex = -1;
            }
        };

        FormClosing += OnFormClosing;
    }

    // ── Add / Rename / Remove ─────────────────────────────────────────

    private void OnAddInstance()
    {
        string suggested = $"Server {_instances.Count + 1}";
        string? name = ShowInputDialog(
            "Enter a name for the new server instance:",
            "Add Server Instance", suggested);
        if (string.IsNullOrWhiteSpace(name)) return;

        name = name.Trim();
        if (_instances.Any(i => i.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
        {
            MessageBox.Show($"An instance named '{name}' already exists.", "Duplicate Name",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        string dir = Path.Combine(InstancesDir, name);
        Directory.CreateDirectory(dir);

        // If there's already a Main Server instance, offer to auto-configure as a client
        var mainInstance = _instances.FirstOrDefault(i =>
            i.Form.ClusterRole == Models.ClusterRole.MainServer);

        if (mainInstance != null)
        {
            var choice = MessageBox.Show(
                $"'{mainInstance.Name}' is configured as Main Server.\n\n" +
                $"Auto-configure '{name}' as a Cluster Client Server?\n\n" +
                "• Yes — pre-fills Role, Server ID, ports, and Main Server connection\n" +
                "• No  — create blank instance (configure manually)",
                "Configure as Client Server?", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);

            if (choice == DialogResult.Cancel) { Directory.Delete(dir); return; }

            if (choice == DialogResult.Yes)
            {
                int offset = _instances.Count; // port/ID offset from main
                var main   = mainInstance.Form.CurrentConfig;
                var client = new Models.ServerConfiguration
                {
                    ServerName              = main.ServerName,
                    ServerPassword          = main.ServerPassword,
                    AdminPassword           = main.AdminPassword,
                    MaxPlayers              = main.MaxPlayers,
                    MapName                 = "DLC_Level01_Main",
                    GamePort                = main.GamePort  + offset,
                    QueryPort               = main.QueryPort + offset,
                    EchoPort                = main.EchoPort  + offset,
                    RconEnabled             = main.RconEnabled,
                    RconPassword            = main.RconPassword,
                    RconAddress             = main.RconAddress,
                    RconPort                = main.RconPort  + offset,
                    ClusterRole             = Models.ClusterRole.ClientServer,
                    ClusterId               = main.ClusterId + offset,
                    ClusterMainPort         = main.ClusterMainPort,
                    ClusterClientConnect    = $"127.0.0.1:{main.ClusterMainPort}",
                    SaveInterval            = main.SaveInterval,
                    PveMode                 = main.PveMode,
                    ServerPermissionMask    = main.ServerPermissionMask,
                    UseAllCores             = main.UseAllCores,
                    CpuAffinity             = main.CpuAffinity,
                    ProcessPriority         = main.ProcessPriority,
                    EnableCrashDetection    = main.EnableCrashDetection,
                    AutoRestart             = main.AutoRestart,
                    MaxRestartAttempts      = main.MaxRestartAttempts,
                    ScheduledRestartEnabled = main.ScheduledRestartEnabled,
                    UseFixedRestartTimes    = main.UseFixedRestartTimes,
                    RestartIntervalHours    = main.RestartIntervalHours,
                    FixedRestartTimes       = main.FixedRestartTimes,
                    RestartWarningMinutes   = main.RestartWarningMinutes,
                    RestartWarningMessage   = main.RestartWarningMessage,
                    AutoBackupEnabled       = main.AutoBackupEnabled,
                    BackupIntervalHours     = main.BackupIntervalHours,
                    BackupKeepCount         = main.BackupKeepCount,
                    EnableDiscordWebhook    = main.EnableDiscordWebhook,
                    DiscordWebhookUrl       = main.DiscordWebhookUrl,
                    NotifyOnStart           = main.NotifyOnStart,
                    NotifyOnStop            = main.NotifyOnStop,
                    NotifyOnCrash           = main.NotifyOnCrash,
                    NotifyOnRestart         = main.NotifyOnRestart,
                    NotifyOnBackup          = main.NotifyOnBackup,
                };

                // Save pre-configured settings into the new instance folder
                var cm = new Services.ConfigurationManager(dir, null!);
                cm.SaveSettings(client);
            }
        }

        AddInstance(name, dir);
        tcInstances.SelectedTab = _instances.Last().Tab;
        SaveInstanceOrder();
    }

    private void OnRenameInstance()
    {
        var current = CurrentInstance;
        if (current == null) return;

        // Can't rename the legacy single-instance pointing directly to InstancesDir
        if (string.Equals(current.RootDir, InstancesDir, StringComparison.OrdinalIgnoreCase))
        {
            MessageBox.Show("This instance cannot be renamed (legacy single-server layout).",
                "Cannot Rename", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        string? newName = ShowInputDialog("Enter a new name:", "Rename Instance", current.Name);
        if (string.IsNullOrWhiteSpace(newName) || newName.Trim() == current.Name) return;

        newName = newName.Trim();
        string newDir = Path.Combine(InstancesDir, newName);
        if (Directory.Exists(newDir))
        {
            MessageBox.Show($"A folder named '{newName}' already exists.", "Name Taken",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (current.Form.IsServerRunning)
        {
            MessageBox.Show("Stop the server before renaming the instance.", "Server Running",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        try
        {
            Directory.Move(current.RootDir, newDir);
            current.Tab.Text = $"  {newName}  ";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Rename failed: {ex.Message}", "Error",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async void OnRemoveInstance()
    {
        var current = CurrentInstance;
        if (current == null) return;

        if (_instances.Count == 1)
        {
            MessageBox.Show("Cannot remove the only server instance.", "At Least One Required",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var confirm = MessageBox.Show(
            $"Remove instance '{current.Name}'?\n\nThis only removes it from the manager — server files on disk are NOT deleted.",
            "Confirm Remove", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
        if (confirm != DialogResult.Yes) return;

        if (current.Form.IsServerRunning)
            await current.Form.ShutdownAsync();

        statusStrip.Items.Remove(current.StatusLabel);
        tcInstances.TabPages.Remove(current.Tab);
        current.Form.Dispose();
        _instances.Remove(current);
        SaveInstanceOrder();
    }

    // ── Shutdown ──────────────────────────────────────────────────────

    private async void OnFormClosing(object? sender, FormClosingEventArgs e)
    {
        var running = _instances.Where(i => i.Form.IsServerRunning).ToList();
        if (running.Count > 0)
        {
            string names = string.Join(", ", running.Select(i => i.Name));
            var result = MessageBox.Show(
                $"The following servers are still running:\n{names}\n\nStop them before closing?",
                "Servers Running", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Warning);

            if (result == DialogResult.Cancel) { e.Cancel = true; return; }
            if (result == DialogResult.Yes)
                foreach (var inst in running)
                    await inst.Form.ShutdownAsync();
        }

        // Dispose all instances
        foreach (var inst in _instances)
            if (!inst.Form.IsDisposed)
                inst.Form.Dispose();
    }

    // ── Helpers ───────────────────────────────────────────────────────

    private InstanceEntry? CurrentInstance =>
        tcInstances.SelectedTab is { } tab
            ? _instances.FirstOrDefault(i => i.Tab == tab)
            : null;

    private void SaveInstanceOrder()
    {
        try { File.WriteAllLines(OrderFile, _instances.Select(i => i.Name)); }
        catch { /* non-critical */ }
    }

    private void SyncStatusStripOrder()
    {
        // Remove all instance labels then re-add in current _instances order
        foreach (var inst in _instances)
            statusStrip.Items.Remove(inst.StatusLabel);
        foreach (var inst in _instances)
            statusStrip.Items.Add(inst.StatusLabel);
    }

    private void ShowAbout()
    {
        MessageBox.Show(
            $"SoulMask Server Manager\nVersion {Application.ProductVersion}\n\n" +
            ".NET 10 · Windows Forms\n\nManage one or more SoulMask dedicated servers from a single window.",
            "About", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private static string? ShowInputDialog(string prompt, string title, string defaultValue = "")
    {
        using var dlg = new Form
        {
            Text            = title,
            Size            = new Size(420, 140),
            FormBorderStyle = FormBorderStyle.FixedDialog,
            StartPosition   = FormStartPosition.CenterParent,
            MaximizeBox     = false,
            MinimizeBox     = false
        };
        var lbl = new Label  { Text = prompt, AutoSize = true, Location = new Point(12, 14) };
        var txt = new TextBox { Location = new Point(12, 38), Width = 380, Text = defaultValue };
        var btn = new Button  { Text = "OK", DialogResult = DialogResult.OK, Location = new Point(317, 68), Size = new Size(75, 26), FlatStyle = FlatStyle.Flat };
        dlg.AcceptButton = btn;
        dlg.Controls.AddRange([lbl, txt, btn]);
        return dlg.ShowDialog() == DialogResult.OK ? txt.Text : null;
    }
}
