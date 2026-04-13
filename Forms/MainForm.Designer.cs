#nullable enable
using SoulMaskServerManager.Helpers;
namespace SoulMaskServerManager.Forms;

partial class MainForm
{
    private System.ComponentModel.IContainer? components = null;

    // ── Menu & Status ────────────────────────────────────────────────
    protected MenuStrip menuStrip;
    protected ToolStripMenuItem menuFile, menuView, menuHelp;
    protected ToolStripMenuItem menuOpenServerFolder, menuOpenBackupFolder, menuExit;
    protected ToolStripMenuItem menuOpenGameIni, menuOpenEngineIni;
    protected ToolStripMenuItem menuThemeDark, menuThemeLight;
    protected StatusStrip statusStrip;
    protected ToolStripStatusLabel tsStatus, tsUptime, tsPlayers, tsNextRestart, tsNextBackup;

    // ── Main Tab ─────────────────────────────────────────────────────
    protected DarkTabControl tabMain;
    protected TabPage tabDashboard, tabSettings, tabConfigEditor,
                      tabMods, tabPlayers, tabAutomation, tabBackups, tabAbout;

    // ── Mods ─────────────────────────────────────────────────────────
    protected ListView lvMods;
    protected TextBox txtModInput;
    protected Button btnAddMod, btnRemoveMod, btnMoveModUp, btnMoveModDown, btnOpenWorkshop, btnSaveMods;
    protected Button btnCheckModUpdates, btnUpdateMods;
    protected Label lblModStatus;

    // ── Dashboard ────────────────────────────────────────────────────
    protected Panel pnlServerControl;
    protected Label lblStatusDot, lblStatus, lblUptime2, lblPlayerCount2;
    protected Button btnServerAction, btnUpdate, btnRestart;
    protected ProgressBar progressBar;
    protected Label lblProgress;
    protected Button btnClearConsole;
    protected CheckBox chkAutoScroll;
    protected TextBox rtbConsole;

    // ── Settings ─────────────────────────────────────────────────────
    protected Panel pnlSettingsScroll;
    protected GroupBox grpIdentity, grpNetwork, grpRcon, grpGameplay, grpPerformance, grpCustomArgs, grpCluster;
    protected ComboBox cmbClusterRole;
    protected NumericUpDown numClusterId, numClusterMainPort;
    protected TextBox txtClusterClientConnect;
    protected Button btnMigrateSave;
    protected Label lblClusterRoleDesc,
                   lblClusterIdRow, lblClusterIdDesc,
                   lblClusterMainPortRow, lblClusterMainPortDesc,
                   lblClusterClientRow, lblClusterClientDesc, lblMigrateDesc;
    protected TextBox txtServerName, txtServerPassword, txtAdminPassword, txtCustomArgs;
    protected TextBox txtRconPassword, txtRconAddress;
    protected NumericUpDown numMaxPlayers, numGamePort, numQueryPort, numEchoPort, numSaveInterval, numRconPort, numServerPermMask;
    protected CheckBox chkPveMode, chkUseAllCores, chkRconEnabled;
    protected ComboBox cmbMap, cmbProcessPriority;
    protected FlowLayoutPanel flpCpuCores;
    protected Button btnSaveSettings, btnReloadSettings;
    protected Label lblDirtyIndicator;

    // ── Gameplay Settings ─────────────────────────────────────────────
    protected TabPage tabGameplay;
    protected ComboBox cmbGameplayPreset;
    protected TextBox txtGameplaySearch;
    protected Button btnSaveGameplay, btnReloadGameplay, btnApplyLive, btnSaveAsPreset, btnDeletePreset, btnResetGameplayDefaults;
    protected Label lblGameplayStatus, lblGameplayDirty, lblGameplayDescription;
    protected DataGridView dgvGameplay;

    // ── Config Editor ─────────────────────────────────────────────────
    protected SplitContainer splitConfig;
    protected Panel pnlGameIniHeader, pnlEngineIniHeader;
    protected Label lblGameIniTitle, lblEngineIniTitle;
    protected Button btnSaveGameIni, btnReloadGameIni, btnOpenGameIni;
    protected Button btnSaveEngineIni, btnReloadEngineIni, btnOpenEngineIni;
    protected RichTextBox rtbGameIni, rtbEngineIni;

    // ── Players ───────────────────────────────────────────────────────
    protected Panel pnlPlayersTop;
    protected Button btnRefreshPlayers, btnKickPlayer, btnBanPlayer, btnMutePlayer, btnMessagePlayer, btnBanList;
    protected CheckBox chkAutoRefreshPlayers;
    protected Label lblRconStatus;
    protected DataGridView dgvPlayers;
    protected Panel pnlAnnounce;
    protected TextBox txtAnnouncement;
    protected Button btnBroadcast;
    protected RichTextBox rtbRconOutput;
    protected Button btnClearRconOutput;

    // ── Automation ────────────────────────────────────────────────────
    protected GroupBox grpSchedule, grpAutoBackup, grpDiscord;
    // Schedule
    protected CheckBox chkScheduleEnabled;
    protected RadioButton rdoInterval, rdoFixedTimes;
    protected NumericUpDown numIntervalHours, numWarningMins;
    protected TextBox txtFixedTimes, txtRestartMessage;
    protected Label lblNextRestart2;
    // Backup
    protected CheckBox chkAutoBackup;
    protected ComboBox cmbBackupInterval;
    protected NumericUpDown numBackupKeep;
    protected Button btnCreateBackupNow2;
    protected Label lblNextBackup2;
    // Discord
    protected CheckBox chkDiscordEnabled;
    protected TextBox txtWebhookUrl;
    protected Button btnTestWebhook;
    protected CheckBox chkNotifyStart, chkNotifyStop, chkNotifyCrash, chkNotifyRestart, chkNotifyBackup;

    // ── Backups ───────────────────────────────────────────────────────
    protected DataGridView dgvBackups;
    protected Button btnCreateBackup, btnRestoreBackup, btnDeleteBackup, btnOpenBackupsFolder;

    // ── About ─────────────────────────────────────────────────────────
    protected Label lblAboutTitle, lblAboutVersion, lblAboutDotNet,
                    lblAboutOs, lblAboutCpu;

    // ── Tooltips ──────────────────────────────────────────────────────
    private ToolTip _toolTip = null!;

    protected override void Dispose(bool disposing)
    {
        if (disposing && components != null) components.Dispose();
        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        components = new System.ComponentModel.Container();
        SuspendLayout();

        // ── Form ─────────────────────────────────────────────────────
        Text            = $"SoulMask Server Manager  v{Application.ProductVersion}";
        Size            = new Size(1180, 760);
        MinimumSize     = new Size(1000, 680);
        StartPosition   = FormStartPosition.CenterScreen;
        Font            = new Font("Segoe UI", 9f, FontStyle.Regular, GraphicsUnit.Point);
        Icon            = Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? SystemIcons.Application;

        // ── MenuStrip ─────────────────────────────────────────────────
        BuildMenuStrip();

        // ── StatusStrip ───────────────────────────────────────────────
        BuildStatusStrip();

        // ── TabControl ────────────────────────────────────────────────
        tabMain = new DarkTabControl
        {
            Dock     = DockStyle.Fill,
            Font     = new Font("Segoe UI", 9.5f),
            Padding  = new Point(14, 4),
            ItemSize = new Size(120, 28)
        };

        tabDashboard   = new TabPage("  Dashboard  ");
        tabSettings    = new TabPage("  Settings  ");
        tabConfigEditor= new TabPage("  Config Files  ");
        tabGameplay    = new TabPage("  Gameplay  ");
        tabMods        = new TabPage("  Mods  ");
        tabPlayers     = new TabPage("  Players  ");
        tabAutomation  = new TabPage("  Automation  ");
        tabBackups     = new TabPage("  Backups  ");
        tabAbout       = new TabPage("  About  ");

        tabMain.TabPages.AddRange([tabDashboard, tabSettings, tabConfigEditor,
                                   tabGameplay, tabMods, tabPlayers, tabAutomation, tabBackups, tabAbout]);

        BuildDashboardTab();
        BuildSettingsTab();
        BuildConfigEditorTab();
        BuildGameplayTab();
        BuildModsTab();
        BuildPlayersTab();
        BuildAutomationTab();
        BuildBackupsTab();
        BuildAboutTab();

        Controls.Add(tabMain);
        Controls.Add(statusStrip);
        Controls.Add(menuStrip);
        MainMenuStrip = menuStrip;

        BuildTooltips();

        ResumeLayout(false);
        PerformLayout();
    }

    // ═════════════════════════════════════════════════════════════════
    // Tooltips
    // ═════════════════════════════════════════════════════════════════
    private void BuildTooltips()
    {
        _toolTip = new ToolTip { AutoPopDelay = 8000, InitialDelay = 400, ReshowDelay = 200 };

        // Dashboard
        _toolTip.SetToolTip(btnServerAction, "Install, start, or stop the server.");
        _toolTip.SetToolTip(btnUpdate,       "Download the latest server files from Steam. Server must be stopped first.");
        _toolTip.SetToolTip(btnRestart,      "Stop and immediately restart the server.");
        _toolTip.SetToolTip(btnClearConsole, "Clear the console log.");
        _toolTip.SetToolTip(chkAutoScroll,   "Automatically scroll the console to the latest line.");

        // Players tab
        _toolTip.SetToolTip(btnRefreshPlayers, "Query the server for the current online player list.");
        _toolTip.SetToolTip(btnKickPlayer,     "Remove the selected player from the server.\nThey can rejoin immediately.");
        _toolTip.SetToolTip(btnBanPlayer,      "Permanently ban the selected player.\nThey cannot rejoin until unbanned.");
        _toolTip.SetToolTip(btnMutePlayer,     "Prevent the selected player from sending chat messages.\nRequires mute list enabled (Perm Mask includes 16).");
        _toolTip.SetToolTip(btnMessagePlayer,  "Broadcast a message to all players on the server.");
        _toolTip.SetToolTip(btnBanList,        "View and manage the server ban list.\nUnban players from here.");
        _toolTip.SetToolTip(btnBroadcast,      "Send the message to all players' system chat.");

        // Settings — Network
        _toolTip.SetToolTip(numGamePort,        "UDP port players connect to. Default: 8777.\nMust be open in your firewall.");
        _toolTip.SetToolTip(numQueryPort,       "Steam query port used for server browser. Default: 27015.\nMust be open in your firewall.");
        _toolTip.SetToolTip(numEchoPort,        "Telnet console port for server commands (Players tab, Automation).\nLoopback only — do NOT forward this port externally.");
        _toolTip.SetToolTip(numServerPermMask,  "Controls which permission lists stay active after a server restart.\n" +
                                                "Add values together for multiple lists:\n" +
                                                "  1 = Account whitelist (only listed players can join)\n" +
                                                "  2 = Ban list (default — keeps bans active)\n" +
                                                "  4 = IP whitelist\n" +
                                                "  8 = IP blacklist\n" +
                                                " 16 = Mute list\n" +
                                                "Example: 18 = bans (2) + mutes (16)");

        // Settings — RCON
        _toolTip.SetToolTip(chkRconEnabled,  "Enable Source RCON for remote admin access from outside the server.\nNot required for the Players tab — that uses EchoPort.");
        _toolTip.SetToolTip(txtRconPassword, "Password required to connect via RCON.");
        _toolTip.SetToolTip(numRconPort,     "TCP port for RCON connections. Default: 19000.");
        _toolTip.SetToolTip(txtRconAddress,  "IP address RCON binds to.\n0.0.0.0 = all network adapters (recommended).");

        // Gameplay tab
        _toolTip.SetToolTip(cmbGameplayPreset,  "Select which preset to view/edit.\n0 = Custom (active server settings), 1 & 2 = built-in presets.");
        _toolTip.SetToolTip(btnSaveGameplay,   "Save changes to the selected preset.\nFor server presets (0/1/2), writes to GameXishu.json.");
        _toolTip.SetToolTip(btnReloadGameplay, "Reload all presets from disk, discarding unsaved changes.");
        _toolTip.SetToolTip(btnSaveAsPreset,   "Save the current values as a new named custom preset.");
        _toolTip.SetToolTip(btnDeletePreset,          "Delete the selected custom preset.\nBuilt-in server presets (0/1/2) cannot be deleted.");
        _toolTip.SetToolTip(btnResetGameplayDefaults, "Reset all values in the current preset to the original game defaults.\nA backup is taken automatically on first load.");
        _toolTip.SetToolTip(btnApplyLive,      "Apply the selected setting to the running server immediately via EchoPort.\nNo restart needed — uses the 'sc' console command.");

        // Settings — Performance
        _toolTip.SetToolTip(chkUseAllCores,      "Use all available CPU cores. Uncheck to manually select cores below.");
        _toolTip.SetToolTip(cmbProcessPriority,  "Windows process priority for the server process.\nAboveNormal or High can improve performance but may affect other processes.");

        // Settings — Cluster
        _toolTip.SetToolTip(cmbClusterRole,
            "Standalone — Normal single server. No cluster arguments added to the launch command.\n\n" +
            "Main Server — Manages player accounts for the entire cluster.\n" +
            "  Adds: -serverid=N  -mainserverport=PORT\n" +
            "  This server must be started first and stopped last.\n" +
            "  If it goes offline, all client servers stop accepting connections.\n\n" +
            "Client Server — Joins an existing cluster managed by a Main Server.\n" +
            "  Adds: -serverid=N  -clientserverconnect=IP:PORT\n" +
            "  Will not accept players until connected to the main server.");
        _toolTip.SetToolTip(numClusterId,
            "Unique numeric ID for this server within the cluster.\n" +
            "Every server in the cluster must have a different ID.\n" +
            "Players use this ID to identify which server they are on.");
        _toolTip.SetToolTip(numClusterMainPort,
            "TCP broadcast port that client servers connect to.\n" +
            "Default: 20000. Keep this port internal — do NOT open it to the public.\n" +
            "Any server that can reach this port can register as a client.");
        _toolTip.SetToolTip(txtClusterClientConnect,
            "IP address and broadcast port of the Main Server.\n" +
            "Format: ip:port  (e.g. 10.10.1.5:20000)\n" +
            "This server will not accept players until it connects to the main server.");
        _toolTip.SetToolTip(btnMigrateSave,
            "Migrates an existing standalone save to work with a cluster.\n" +
            "Runs CopyRoles.exe to copy player account data from world.db → account.db.\n" +
            "Required if you are converting an existing server into a cluster main server.\n" +
            "Not needed for fresh cluster installs.");
    }

    // ═════════════════════════════════════════════════════════════════
    // Menu
    // ═════════════════════════════════════════════════════════════════
    private void BuildMenuStrip()
    {
        menuStrip = new MenuStrip();

        menuFile = new ToolStripMenuItem("File");
        menuOpenServerFolder = new ToolStripMenuItem("Open Server Folder");
        menuOpenBackupFolder = new ToolStripMenuItem("Open Backup Folder");
        menuOpenGameIni      = new ToolStripMenuItem("Edit Game.ini");
        menuOpenEngineIni    = new ToolStripMenuItem("Edit Engine.ini");
        menuExit             = new ToolStripMenuItem("Exit");
        menuFile.DropDownItems.AddRange([menuOpenServerFolder, menuOpenBackupFolder,
            new ToolStripSeparator(), menuOpenGameIni, menuOpenEngineIni,
            new ToolStripSeparator(), menuExit]);

        menuView = new ToolStripMenuItem("View");
        menuThemeDark    = new ToolStripMenuItem("🌙  Dark Theme")  { Tag = "dark" };
        menuThemeLight   = new ToolStripMenuItem("☀️  Light Theme") { Tag = "light" };
        menuView.DropDownItems.AddRange([menuThemeDark, menuThemeLight]);

        menuHelp = new ToolStripMenuItem("Help");
        var menuAbout = new ToolStripMenuItem("About");
        menuHelp.DropDownItems.Add(menuAbout);

        menuStrip.Items.AddRange([menuFile, menuView, menuHelp]);
    }

    // ═════════════════════════════════════════════════════════════════
    // Status strip
    // ═════════════════════════════════════════════════════════════════
    private void BuildStatusStrip()
    {
        statusStrip = new StatusStrip { SizingGrip = false };

        tsStatus      = new ToolStripStatusLabel("Status: Not Installed") { Spring = false, AutoSize = true };
        tsUptime      = new ToolStripStatusLabel("Uptime: --:--:--")       { AutoSize = true, Margin = new Padding(20, 0, 0, 0) };
        tsPlayers     = new ToolStripStatusLabel("Players: 0")             { AutoSize = true, Margin = new Padding(20, 0, 0, 0) };
        tsNextRestart = new ToolStripStatusLabel("Next Restart: ---")      { AutoSize = true, Margin = new Padding(20, 0, 0, 0) };
        tsNextBackup  = new ToolStripStatusLabel("Next Backup: ---")       { AutoSize = true, Margin = new Padding(20, 0, 0, 0), Spring = true };

        statusStrip.Items.AddRange([tsStatus, tsUptime, tsPlayers, tsNextRestart, tsNextBackup]);
    }

    // ═════════════════════════════════════════════════════════════════
    // Dashboard tab
    // ═════════════════════════════════════════════════════════════════
    private void BuildDashboardTab()
    {
        tabDashboard.Padding = new Padding(0);

        // ── Control panel (top) ──────────────────────────────────────
        pnlServerControl = new Panel
        {
            Dock    = DockStyle.Top,
            Height  = 130,
            Padding = new Padding(12, 10, 12, 8)
        };

        // Status indicator dot
        lblStatusDot = new Label
        {
            Text      = "●",
            Font      = new Font("Segoe UI", 18f, FontStyle.Bold),
            ForeColor = Color.Gray,
            AutoSize  = true,
            Location  = new Point(12, 12),
            Tag       = "status_dot"
        };

        lblStatus = new Label
        {
            Text     = "Not Installed",
            Font     = new Font("Segoe UI", 11f, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(44, 14)
        };

        lblUptime2 = new Label
        {
            Text     = "Uptime: --:--:--",
            AutoSize = true,
            Location = new Point(44, 36),
            Tag      = "sub"
        };

        lblPlayerCount2 = new Label
        {
            Text     = "Players: 0",
            AutoSize = true,
            Location = new Point(200, 36),
            Tag      = "sub"
        };

        // Action Buttons
        btnServerAction = new Button
        {
            Text     = "Install Server",
            Size     = new Size(160, 42),
            Location = new Point(12, 60),
            Tag      = "accent",
            Font     = new Font("Segoe UI", 10f, FontStyle.Bold),
            FlatStyle = FlatStyle.Flat
        };

        btnUpdate = new Button
        {
            Text     = "Update Server",
            Size     = new Size(145, 42),
            Location = new Point(184, 60),
            FlatStyle = FlatStyle.Flat
        };

        btnRestart = new Button
        {
            Text     = "Restart",
            Size     = new Size(110, 42),
            Location = new Point(341, 60),
            Enabled  = false,
            FlatStyle = FlatStyle.Flat
        };

        // Progress — own row below the buttons (y=108), hidden by default
        progressBar = new ProgressBar
        {
            Location = new Point(8, 110),
            Size     = new Size(500, 14),
            Visible  = false,
            Style    = ProgressBarStyle.Continuous
        };

        lblProgress = new Label
        {
            Text     = "",
            Location = new Point(518, 108),
            Size     = new Size(300, 18),
            Visible  = false,
            Tag      = "sub"
        };

        // Clear Log + Auto-scroll on the same row as the action buttons
        btnClearConsole = new Button
        {
            Text      = "Clear Log",
            Size      = new Size(80, 42),
            Location  = new Point(464, 60),
            FlatStyle = FlatStyle.Flat
        };

        chkAutoScroll = new CheckBox
        {
            Text     = "Auto-scroll",
            Checked  = true,
            Location = new Point(556, 72),
            AutoSize = true
        };

        pnlServerControl.Controls.AddRange([lblStatusDot, lblStatus, lblUptime2,
            lblPlayerCount2, btnServerAction, btnUpdate, btnRestart,
            progressBar, lblProgress, btnClearConsole, chkAutoScroll]);

        // ── Console TextBox fills all remaining space ──
        // IMPORTANT: Add Fill control FIRST, Top control SECOND.
        // WinForms docking processes from last→first in the Controls collection,
        // so Top must be at a higher index to be processed before Fill.
        rtbConsole = new TextBox
        {
            Dock        = DockStyle.Fill,
            ReadOnly    = true,
            Multiline   = true,
            BackColor   = Color.FromArgb(12, 12, 12),
            ForeColor   = Color.FromArgb(204, 204, 204),
            Font        = new Font("Consolas", 9f, FontStyle.Regular, GraphicsUnit.Point),
            WordWrap    = true,
            ScrollBars  = ScrollBars.Vertical,
            BorderStyle = BorderStyle.None,
            Tag         = "console"
        };
        tabDashboard.Controls.Add(rtbConsole);       // Fill — index 0, processed last
        tabDashboard.Controls.Add(pnlServerControl); // Top  — index 1, processed first
    }

    // ═════════════════════════════════════════════════════════════════
    // Settings tab
    // ═════════════════════════════════════════════════════════════════
    private void BuildSettingsTab()
    {
        tabSettings.Padding = new Padding(0);

        pnlSettingsScroll = new Panel
        {
            Dock      = DockStyle.Fill,
            AutoScroll = true,
            Padding   = new Padding(10, 10, 10, 10)
        };

        // ── Server Identity ─────────────────────────────────────────
        grpIdentity = MakeGroupBox("Server Identity", 10, 10, 520, 160);
        AddLabel(grpIdentity, "Server Name:", 10, 24);
        txtServerName = AddTextBox(grpIdentity, 140, 21, 360, "My SoulMask Server");
        AddLabel(grpIdentity, "Max Players:", 10, 54);
        numMaxPlayers = AddNumeric(grpIdentity, 140, 51, 1, 200, 70);
        AddLabel(grpIdentity, "Admin Password:", 10, 84);
        txtAdminPassword = AddTextBox(grpIdentity, 140, 81, 200, "");
        txtAdminPassword.UseSystemPasswordChar = true;
        AddLabel(grpIdentity, "Server Password:", 10, 114);
        txtServerPassword = AddTextBox(grpIdentity, 140, 111, 200, "");
        txtServerPassword.UseSystemPasswordChar = true;
        AddLabel(grpIdentity, "(leave blank = public)", 348, 114, tag: "sub");

        // ── Network ─────────────────────────────────────────────────
        grpNetwork = MakeGroupBox("Network", 544, 10, 380, 162);
        AddLabel(grpNetwork, "Game Port:", 10, 24);
        numGamePort = AddNumeric(grpNetwork, 140, 21, 1024, 65535, 8777);
        AddLabel(grpNetwork, "Query Port:", 10, 54);
        numQueryPort = AddNumeric(grpNetwork, 140, 51, 1024, 65535, 27015);
        AddLabel(grpNetwork, "Echo Port:", 10, 84);
        numEchoPort = AddNumeric(grpNetwork, 140, 81, 1024, 65535, 18888);
        AddLabel(grpNetwork, "Perm Mask:", 10, 114);
        numServerPermMask = AddNumeric(grpNetwork, 140, 111, 0, 31, 2);
        AddLabel(grpNetwork, "2=bans  3=whitelist+bans  16=mutes", 230, 114, tag: "sub");

        // ── RCON ────────────────────────────────────────────────────
        grpRcon = MakeGroupBox("RCON (Remote Admin)", 544, 184, 380, 148);
        chkRconEnabled = new CheckBox { Text = "Enable RCON", Location = new Point(140, 22), AutoSize = true };
        grpRcon.Controls.Add(chkRconEnabled);
        AddLabel(grpRcon, "Enable RCON:", 10, 24);
        AddLabel(grpRcon, "RCON Password:", 10, 54);
        txtRconPassword = AddTextBox(grpRcon, 140, 51, 220, "");
        txtRconPassword.UseSystemPasswordChar = true;
        AddLabel(grpRcon, "RCON Port:", 10, 84);
        numRconPort = AddNumeric(grpRcon, 140, 81, 1024, 65535, 19000);
        AddLabel(grpRcon, "Bind IP:", 10, 114);
        txtRconAddress = AddTextBox(grpRcon, 140, 111, 150, "0.0.0.0");
        AddLabel(grpRcon, "0.0.0.0 = all adapters", 298, 114, tag: "sub");

        // ── Gameplay ────────────────────────────────────────────────
        grpGameplay = MakeGroupBox("Gameplay", 10, 346, 360, 110);
        AddLabel(grpGameplay, "Map:", 10, 24);
        cmbMap = new ComboBox
        {
            Location      = new Point(140, 21),
            Size          = new Size(200, 23),
            DropDownStyle = ComboBoxStyle.DropDown
        };
        cmbMap.Items.AddRange(["Level01_Main", "DLC_Level01_Main"]);
        cmbMap.SelectedIndex = 0;
        grpGameplay.Controls.Add(cmbMap);
        AddLabel(grpGameplay, "Save Interval (sec):", 10, 54);
        numSaveInterval = AddNumeric(grpGameplay, 180, 51, 60, 3600, 600);
        chkPveMode = new CheckBox { Text = "PvE Mode", Location = new Point(10, 82), AutoSize = true };
        grpGameplay.Controls.Add(chkPveMode);

        // ── Performance ──────────────────────────────────────────────
        grpPerformance = MakeGroupBox("Performance", 382, 346, 542, 110);
        AddLabel(grpPerformance, "Process Priority:", 10, 24);
        cmbProcessPriority = new ComboBox
        {
            Location      = new Point(150, 21),
            Size          = new Size(150, 23),
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        cmbProcessPriority.Items.AddRange(["Normal", "AboveNormal", "High"]);
        cmbProcessPriority.SelectedIndex = 0;
        grpPerformance.Controls.Add(cmbProcessPriority);
        chkUseAllCores = new CheckBox { Text = "Use All CPU Cores", Checked = true, Location = new Point(10, 54), AutoSize = true };
        grpPerformance.Controls.Add(chkUseAllCores);
        AddLabel(grpPerformance, "CPU Affinity:", 10, 80);
        flpCpuCores = new FlowLayoutPanel
        {
            Location    = new Point(150, 76),
            Size        = new Size(380, 24),
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            AutoSize    = false
        };
        grpPerformance.Controls.Add(flpCpuCores);

        // ── Custom Args ──────────────────────────────────────────────
        grpCustomArgs = MakeGroupBox("Additional Launch Arguments", 10, 468, 914, 60);
        txtCustomArgs = new TextBox { Location = new Point(10, 24), Size = new Size(884, 23), PlaceholderText = "-ExtraArg1 -ExtraArg2  (appended to launch command)" };
        grpCustomArgs.Controls.Add(txtCustomArgs);

        // ── Cluster ──────────────────────────────────────────────────
        grpCluster = MakeGroupBox("Server Cluster", 10, 540, 914, 226);

        AddLabel(grpCluster, "Cluster Role:", 10, 24);
        cmbClusterRole = new ComboBox
        {
            Location      = new Point(160, 21),
            Size          = new Size(180, 23),
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        cmbClusterRole.Items.AddRange(["Standalone", "Main Server", "Client Server"]);
        cmbClusterRole.SelectedIndex = 0;
        grpCluster.Controls.Add(cmbClusterRole);
        lblClusterRoleDesc = AddLabel(grpCluster, "Standalone = no cluster args added to launch command", 352, 24, tag: "sub");

        lblClusterIdRow  = AddLabel(grpCluster, "Server ID:", 10, 54);
        numClusterId     = AddNumeric(grpCluster, 160, 51, 1, 999, 1);
        lblClusterIdDesc = AddLabel(grpCluster, "Unique numeric ID within the cluster", 280, 54, tag: "sub");

        // Main Server row
        lblClusterMainPortRow  = AddLabel(grpCluster, "Broadcast Port:", 10, 84);
        numClusterMainPort     = AddNumeric(grpCluster, 160, 81, 1024, 65535, 20000);
        lblClusterMainPortDesc = AddLabel(grpCluster, "TCP port client servers connect to (not public-facing)", 280, 84, tag: "sub");

        // Client Server row (same y — only one is visible at a time)
        lblClusterClientRow  = AddLabel(grpCluster, "Main Server:", 10, 84);
        txtClusterClientConnect = new TextBox { Location = new Point(160, 81), Size = new Size(200, 23), PlaceholderText = "10.10.1.5:20000" };
        grpCluster.Controls.Add(txtClusterClientConnect);
        lblClusterClientDesc = AddLabel(grpCluster, "IP:port of the main server broadcast port", 372, 84, tag: "sub");

        btnMigrateSave = new Button
        {
            Text      = "Migrate Existing Save…",
            Size      = new Size(180, 28),
            Location  = new Point(10, 112),
            FlatStyle = FlatStyle.Flat
        };
        grpCluster.Controls.Add(btnMigrateSave);
        lblMigrateDesc = AddLabel(grpCluster, "Copies player data from world.db → account.db using CopyRoles.exe (required for existing saves)", 202, 116, tag: "sub");



        // ── Save Row ─────────────────────────────────────────────────
        btnSaveSettings = new Button
        {
            Text      = "Save Settings",
            Size      = new Size(140, 34),
            Location  = new Point(10, 780),
            Tag       = "accent",
            FlatStyle = FlatStyle.Flat,
            Font      = new Font("Segoe UI", 9.5f, FontStyle.Bold)
        };
        btnReloadSettings = new Button
        {
            Text      = "Reload",
            Size      = new Size(90, 34),
            Location  = new Point(160, 780),
            FlatStyle = FlatStyle.Flat
        };
        lblDirtyIndicator = new Label
        {
            Text      = "● Unsaved changes",
            ForeColor = Color.FromArgb(255, 152, 0),
            AutoSize  = true,
            Location  = new Point(264, 788),
            Visible   = false
        };

        pnlSettingsScroll.Controls.AddRange([grpIdentity, grpNetwork, grpRcon, grpGameplay,
            grpPerformance, grpCustomArgs, grpCluster, btnSaveSettings, btnReloadSettings, lblDirtyIndicator]);
        tabSettings.Controls.Add(pnlSettingsScroll);
    }

    // ═════════════════════════════════════════════════════════════════
    // Config Editor tab
    // ═════════════════════════════════════════════════════════════════
    private void BuildConfigEditorTab()
    {
        splitConfig = new SplitContainer
        {
            Dock        = DockStyle.Fill,
            Orientation = Orientation.Horizontal,
            SplitterDistance = 320,
            Panel1MinSize    = 100,
            Panel2MinSize    = 100
        };

        // ── Game.ini ────────────────────────────────────────────────────────
        // Use a FlowLayoutPanel so label + buttons never overlap regardless of
        // font size or window width.
        pnlGameIniHeader = new Panel { Dock = DockStyle.Fill };
        var flpGame = new FlowLayoutPanel
        {
            Dock          = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents  = false,
            Padding       = new Padding(6, 4, 4, 4)
        };
        lblGameIniTitle  = new Label  { Text = "Game.ini", Font = new Font("Segoe UI", 9.5f, FontStyle.Bold), AutoSize = true, Margin = new Padding(0, 5, 14, 0) };
        btnSaveGameIni   = new Button { Text = "Save",           Size = new Size(72, 28), FlatStyle = FlatStyle.Flat, Margin = new Padding(0, 0, 4, 0) };
        btnReloadGameIni = new Button { Text = "Reload",         Size = new Size(72, 28), FlatStyle = FlatStyle.Flat, Margin = new Padding(0, 0, 4, 0) };
        btnOpenGameIni   = new Button { Text = "Open in Editor", Size = new Size(128, 28), FlatStyle = FlatStyle.Flat };
        flpGame.Controls.AddRange([lblGameIniTitle, btnSaveGameIni, btnReloadGameIni, btnOpenGameIni]);
        pnlGameIniHeader.Controls.Add(flpGame);

        rtbGameIni = new RichTextBox
        {
            Dock = DockStyle.Fill,
            Font = new Font("Cascadia Mono", 9f),
            AcceptsTab = true,
            ScrollBars = RichTextBoxScrollBars.Both,
            WordWrap   = false
        };

        var tlpGame = new TableLayoutPanel
        {
            Dock            = DockStyle.Fill,
            ColumnCount     = 1,
            RowCount        = 2,
            Padding         = new Padding(0),
            CellBorderStyle = TableLayoutPanelCellBorderStyle.None
        };
        tlpGame.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        tlpGame.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));  // header row
        tlpGame.RowStyles.Add(new RowStyle(SizeType.Percent, 100));  // editor row
        tlpGame.Controls.Add(pnlGameIniHeader, 0, 0);
        tlpGame.Controls.Add(rtbGameIni, 0, 1);
        splitConfig.Panel1.Controls.Add(tlpGame);

        // ── Engine.ini ──────────────────────────────────────────────────────
        pnlEngineIniHeader = new Panel { Dock = DockStyle.Fill };
        var flpEngine = new FlowLayoutPanel
        {
            Dock          = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents  = false,
            Padding       = new Padding(6, 4, 4, 4)
        };
        lblEngineIniTitle  = new Label  { Text = "Engine.ini", Font = new Font("Segoe UI", 9.5f, FontStyle.Bold), AutoSize = true, Margin = new Padding(0, 5, 14, 0) };
        btnSaveEngineIni   = new Button { Text = "Save",           Size = new Size(72, 28), FlatStyle = FlatStyle.Flat, Margin = new Padding(0, 0, 4, 0) };
        btnReloadEngineIni = new Button { Text = "Reload",         Size = new Size(72, 28), FlatStyle = FlatStyle.Flat, Margin = new Padding(0, 0, 4, 0) };
        btnOpenEngineIni   = new Button { Text = "Open in Editor", Size = new Size(128, 28), FlatStyle = FlatStyle.Flat };
        flpEngine.Controls.AddRange([lblEngineIniTitle, btnSaveEngineIni, btnReloadEngineIni, btnOpenEngineIni]);
        pnlEngineIniHeader.Controls.Add(flpEngine);

        rtbEngineIni = new RichTextBox
        {
            Dock = DockStyle.Fill,
            Font = new Font("Cascadia Mono", 9f),
            AcceptsTab = true,
            ScrollBars = RichTextBoxScrollBars.Both,
            WordWrap   = false
        };

        var tlpEngine = new TableLayoutPanel
        {
            Dock            = DockStyle.Fill,
            ColumnCount     = 1,
            RowCount        = 2,
            Padding         = new Padding(0),
            CellBorderStyle = TableLayoutPanelCellBorderStyle.None
        };
        tlpEngine.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        tlpEngine.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));  // header row
        tlpEngine.RowStyles.Add(new RowStyle(SizeType.Percent, 100));  // editor row
        tlpEngine.Controls.Add(pnlEngineIniHeader, 0, 0);
        tlpEngine.Controls.Add(rtbEngineIni, 0, 1);
        splitConfig.Panel2.Controls.Add(tlpEngine);

        tabConfigEditor.Controls.Add(splitConfig);
    }

    // ═════════════════════════════════════════════════════════════════
    // Gameplay Settings tab
    // ═════════════════════════════════════════════════════════════════
    private void BuildGameplayTab()
    {
        tabGameplay.Padding = new Padding(0);

        // ── Top toolbar ──────────────────────────────────────────────
        var pnlTop = new Panel { Dock = DockStyle.Top, Height = 50, Padding = new Padding(8, 8, 8, 4) };

        AddLabel(pnlTop, "Preset:", 8, 16);
        cmbGameplayPreset = new ComboBox
        {
            Location      = new Point(58, 12),
            Size          = new Size(160, 23),
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        // Items populated in LoadGameplaySettings()

        AddLabel(pnlTop, "Search:", 232, 16);
        txtGameplaySearch = new TextBox
        {
            Location        = new Point(286, 12),
            Size            = new Size(170, 23),
            PlaceholderText = "Filter settings..."
        };

        btnSaveGameplay         = new Button { Text = "Save",             Size = new Size(72, 30), Location = new Point(470, 10), FlatStyle = FlatStyle.Flat, Tag = "accent" };
        btnReloadGameplay       = new Button { Text = "Reload",           Size = new Size(72, 30), Location = new Point(550, 10), FlatStyle = FlatStyle.Flat };
        btnSaveAsPreset         = new Button { Text = "Save As…",         Size = new Size(80, 30), Location = new Point(630, 10), FlatStyle = FlatStyle.Flat };
        btnDeletePreset         = new Button { Text = "Delete",           Size = new Size(70, 30), Location = new Point(718, 10), FlatStyle = FlatStyle.Flat, Tag = "danger", Enabled = false };
        btnResetGameplayDefaults= new Button { Text = "Reset Defaults",   Size = new Size(110, 30), Location = new Point(796, 10), FlatStyle = FlatStyle.Flat };
        btnApplyLive            = new Button { Text = "Apply Live",       Size = new Size(88, 30),  Location = new Point(914, 10), FlatStyle = FlatStyle.Flat };

        lblGameplayDirty  = new Label { Text = "● Unsaved", ForeColor = Color.FromArgb(255, 152, 0), AutoSize = true, Location = new Point(1014, 17), Visible = false };
        lblGameplayStatus = new Label { Text = "", AutoSize = true, Location = new Point(1080, 17), Tag = "sub" };

        pnlTop.Controls.AddRange([cmbGameplayPreset, txtGameplaySearch,
            btnSaveGameplay, btnReloadGameplay, btnSaveAsPreset, btnDeletePreset,
            btnResetGameplayDefaults, btnApplyLive, lblGameplayDirty, lblGameplayStatus]);

        // ── Settings grid ────────────────────────────────────────────
        dgvGameplay = new DataGridView
        {
            Dock                        = DockStyle.Fill,
            AllowUserToAddRows          = false,
            AllowUserToDeleteRows       = false,
            SelectionMode               = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect                 = false,
            RowHeadersVisible           = false,
            AutoSizeColumnsMode         = DataGridViewAutoSizeColumnsMode.Fill,
            BorderStyle                 = BorderStyle.None,
            ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
            ColumnHeadersHeight         = 30,
            EditMode                    = DataGridViewEditMode.EditOnKeystrokeOrF2,
            ClipboardCopyMode           = DataGridViewClipboardCopyMode.EnableWithoutHeaderText
        };
        dgvGameplay.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Setting", HeaderText = "Setting", ReadOnly = true, FillWeight = 65
        });
        dgvGameplay.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Value", HeaderText = "Value", FillWeight = 35
        });

        // ── Description bar ──────────────────────────────────────────
        var pnlDesc = new Panel { Dock = DockStyle.Bottom, Height = 36, Padding = new Padding(8, 0, 8, 0) };
        lblGameplayDescription = new Label
        {
            Dock      = DockStyle.Fill,
            AutoSize  = false,
            TextAlign = ContentAlignment.MiddleLeft,
            Text      = "Select a setting to see its description.",
            Tag       = "sub"
        };
        pnlDesc.Controls.Add(lblGameplayDescription);

        // Fill FIRST, Bottom SECOND, Top THIRD (WinForms docking rule)
        tabGameplay.Controls.Add(dgvGameplay);
        tabGameplay.Controls.Add(pnlDesc);
        tabGameplay.Controls.Add(pnlTop);
    }

    // ═════════════════════════════════════════════════════════════════
    // Mods tab
    // ═════════════════════════════════════════════════════════════════
    private void BuildModsTab()
    {
        // ── Top: input row ────────────────────────────────────────────
        var pnlTop = new Panel { Dock = DockStyle.Top, Height = 48 };

        var tlpInput = new TableLayoutPanel
        {
            Dock        = DockStyle.None,
            Location    = new Point(12, 10),
            Anchor      = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right,
            Height      = 28,
            ColumnCount = 2,
            RowCount    = 1,
            Margin      = Padding.Empty,
            Padding     = Padding.Empty
        };
        tlpInput.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        tlpInput.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100f));

        txtModInput = new TextBox
        {
            PlaceholderText = "Enter mod ID(s) — comma-separated  (e.g. 3324057706,3330908154)",
            Font   = new Font("Segoe UI", 9.5f),
            Dock   = DockStyle.Fill,
            Margin = new Padding(0, 0, 6, 0)
        };

        btnAddMod = new Button
        {
            Text      = "Add Mod",
            Dock      = DockStyle.Fill,
            FlatStyle = FlatStyle.Flat,
            Margin    = Padding.Empty,
            Tag       = "accent"
        };

        tlpInput.Controls.Add(txtModInput, 0, 0);
        tlpInput.Controls.Add(btnAddMod,   1, 0);

        // Keep TableLayoutPanel full width when tab resizes
        pnlTop.Resize += (_, _) => tlpInput.Width = pnlTop.ClientSize.Width - 24;
        tlpInput.Width = 700;

        pnlTop.Controls.Add(tlpInput);

        // ── List ──────────────────────────────────────────────────────
        lvMods = new ListView
        {
            Dock          = DockStyle.Fill,
            View          = View.Details,
            FullRowSelect = true,
            GridLines     = false,
            HeaderStyle   = ColumnHeaderStyle.Nonclickable,
            MultiSelect   = false,   // single-select makes drag/move unambiguous
            BorderStyle   = BorderStyle.None,
            Font          = new Font("Segoe UI", 9.5f),
            AllowDrop     = true
        };
        lvMods.Columns.Add("Order",  56);
        lvMods.Columns.Add("Mod ID", 130);
        lvMods.Columns.Add("Name",   220);
        lvMods.Columns.Add("Status", 200);

        // Keep Status column filling available width so text never clips
        lvMods.Resize += (_, _) =>
        {
            if (lvMods.Columns.Count >= 4)
                lvMods.Columns[3].Width = Math.Max(160,
                    lvMods.ClientSize.Width
                    - lvMods.Columns[0].Width
                    - lvMods.Columns[1].Width
                    - lvMods.Columns[2].Width - 4);
        };

        // ── Bottom: actions (two rows) ────────────────────────────────
        var pnlBottom = new Panel { Dock = DockStyle.Bottom, Height = 80 };

        // Row 1 — order / remove / workshop / save
        btnMoveModUp = new Button
        {
            Text      = "▲  Up",
            Size      = new Size(80, 26),
            Location  = new Point(12, 8),
            FlatStyle = FlatStyle.Flat
        };
        btnMoveModDown = new Button
        {
            Text      = "▼  Down",
            Size      = new Size(80, 26),
            Location  = new Point(100, 8),
            FlatStyle = FlatStyle.Flat
        };
        btnRemoveMod = new Button
        {
            Text      = "Remove",
            Size      = new Size(80, 26),
            Location  = new Point(192, 8),
            FlatStyle = FlatStyle.Flat
        };
        btnOpenWorkshop = new Button
        {
            Text      = "Open in Workshop",
            Size      = new Size(140, 26),
            Location  = new Point(284, 8),
            FlatStyle = FlatStyle.Flat
        };
        btnSaveMods = new Button
        {
            Text      = "Save",
            Size      = new Size(80, 26),
            Location  = new Point(436, 8),
            FlatStyle = FlatStyle.Flat,
            Tag       = "accent"
        };
        lblModStatus = new Label
        {
            Text      = "No mods",
            AutoSize  = true,
            Location  = new Point(528, 13),
            Tag       = "sub"
        };

        // Row 2 — update checks
        btnCheckModUpdates = new Button
        {
            Text      = "Check for Updates",
            Size      = new Size(140, 26),
            Location  = new Point(12, 46),
            FlatStyle = FlatStyle.Flat
        };
        btnUpdateMods = new Button
        {
            Text      = "Update Mods",
            Size      = new Size(120, 26),
            Location  = new Point(160, 46),
            FlatStyle = FlatStyle.Flat,
            Tag       = "accent"
        };

        pnlBottom.Controls.AddRange([btnMoveModUp, btnMoveModDown, btnRemoveMod,
            btnOpenWorkshop, btnSaveMods, lblModStatus,
            btnCheckModUpdates, btnUpdateMods]);

        // Fill first, then Top/Bottom (WinForms docking processes last→first)
        tabMods.Controls.AddRange([lvMods, pnlBottom, pnlTop]);
    }

    // ═════════════════════════════════════════════════════════════════
    // Players tab
    // ═════════════════════════════════════════════════════════════════
    private void BuildPlayersTab()
    {
        // Top toolbar
        pnlPlayersTop = new Panel { Dock = DockStyle.Top, Height = 46, Padding = new Padding(8, 6, 8, 6) };

        btnRefreshPlayers     = new Button   { Text = "Refresh",          Size = new Size(90, 30),  Location = new Point(8,   7), FlatStyle = FlatStyle.Flat };
        btnKickPlayer         = new Button   { Text = "Kick",             Size = new Size(75, 30),  Location = new Point(106, 7), FlatStyle = FlatStyle.Flat, Enabled = false };
        btnBanPlayer          = new Button   { Text = "Ban",              Size = new Size(75, 30),  Location = new Point(189, 7), FlatStyle = FlatStyle.Flat, Enabled = false, Tag = "danger" };
        btnMutePlayer         = new Button   { Text = "Mute",             Size = new Size(75, 30),  Location = new Point(272, 7), FlatStyle = FlatStyle.Flat, Enabled = false };
        btnMessagePlayer      = new Button   { Text = "Send Message",     Size = new Size(115, 30), Location = new Point(355, 7), FlatStyle = FlatStyle.Flat, Enabled = false };
        btnBanList            = new Button   { Text = "Ban List",         Size = new Size(80, 30),  Location = new Point(478, 7), FlatStyle = FlatStyle.Flat };
        chkAutoRefreshPlayers = new CheckBox { Text = "Auto-refresh 30s", AutoSize = true,          Location = new Point(566, 13), FlatStyle = FlatStyle.Flat };
        lblRconStatus         = new Label    { Text = "EchoPort: —",      AutoSize = true,          Location = new Point(700, 13), Tag = "sub" };

        pnlPlayersTop.Controls.AddRange([btnRefreshPlayers, btnKickPlayer,
            btnBanPlayer, btnMutePlayer, btnMessagePlayer, btnBanList,
            chkAutoRefreshPlayers, lblRconStatus]);

        // Player grid
        dgvPlayers = new DataGridView
        {
            Dock                          = DockStyle.Top,
            Height                        = 280,
            AllowUserToAddRows            = false,
            AllowUserToDeleteRows         = false,
            ReadOnly                      = true,
            SelectionMode                 = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect                   = false,
            RowHeadersVisible             = false,
            AutoSizeColumnsMode           = DataGridViewAutoSizeColumnsMode.Fill,
            BorderStyle                   = BorderStyle.None,
            ColumnHeadersHeightSizeMode   = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
            ColumnHeadersHeight           = 30
        };
        dgvPlayers.Columns.Add(new DataGridViewTextBoxColumn { Name = "Name",    HeaderText = "Player Name",  FillWeight = 50 });
        dgvPlayers.Columns.Add(new DataGridViewTextBoxColumn { Name = "SteamId", HeaderText = "Steam ID",     FillWeight = 50 });

        // Announce row
        pnlAnnounce = new Panel { Dock = DockStyle.Top, Height = 42, Padding = new Padding(8, 6, 8, 6) };
        var lblAnnounce = new Label { Text = "Broadcast:", AutoSize = true, Location = new Point(8, 14) };
        txtAnnouncement = new TextBox { Location = new Point(90, 10), Size = new Size(500, 23), PlaceholderText = "Message to all players..." };
        btnBroadcast    = new Button  { Text = "Send", Size = new Size(70, 24), Location = new Point(598, 9), FlatStyle = FlatStyle.Flat, Tag = "accent" };
        pnlAnnounce.Controls.AddRange([lblAnnounce, txtAnnouncement, btnBroadcast]);

        // RCON output
        var pnlRconBottom = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8, 4, 8, 8) };
        var pnlRconHdr    = new Panel { Dock = DockStyle.Top, Height = 36 };
        var flpRcon       = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, Padding = new Padding(4, 4, 4, 0) };
        var lblRconOut    = new Label { Text = "RCON Output", Font = new Font("Segoe UI", 9f, FontStyle.Bold), AutoSize = true, Margin = new Padding(0, 5, 14, 0) };
        btnClearRconOutput = new Button { Text = "Clear", Size = new Size(65, 26), FlatStyle = FlatStyle.Flat, Margin = new Padding(0, 0, 4, 0) };
        flpRcon.Controls.AddRange([lblRconOut, btnClearRconOutput]);
        pnlRconHdr.Controls.Add(flpRcon);

        rtbRconOutput = new RichTextBox
        {
            Dock      = DockStyle.Fill,
            ReadOnly  = true,
            Font      = new Font("Cascadia Mono", 9f),
            Tag       = "console"
        };
        pnlRconBottom.Controls.Add(rtbRconOutput);
        pnlRconBottom.Controls.Add(pnlRconHdr);

        tabPlayers.Controls.Add(pnlRconBottom);
        tabPlayers.Controls.Add(pnlAnnounce);
        tabPlayers.Controls.Add(dgvPlayers);
        tabPlayers.Controls.Add(pnlPlayersTop);
    }

    // ═════════════════════════════════════════════════════════════════
    // Automation tab
    // ═════════════════════════════════════════════════════════════════
    private void BuildAutomationTab()
    {
        var pnlAuto = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(10) };

        // ── Scheduled Restart ────────────────────────────────────────
        grpSchedule = MakeGroupBox("Scheduled Restart", 10, 10, 420, 250);

        chkScheduleEnabled = new CheckBox { Text = "Enable Scheduled Restarts", Location = new Point(10, 24), AutoSize = true, Font = new Font("Segoe UI", 9f, FontStyle.Bold) };
        grpSchedule.Controls.Add(chkScheduleEnabled);

        rdoInterval   = new RadioButton { Text = "Restart every", Location = new Point(10, 52), AutoSize = true, Checked = true };
        numIntervalHours = new NumericUpDown { Location = new Point(140, 49), Size = new Size(60, 23), Minimum = 1, Maximum = 168, Value = 6 };
        AddLabel(grpSchedule, "hours", 210, 52);
        grpSchedule.Controls.AddRange([rdoInterval, numIntervalHours]);

        rdoFixedTimes = new RadioButton { Text = "Fixed times (HH:mm, comma-separated):", Location = new Point(10, 82), AutoSize = true };
        txtFixedTimes = new TextBox { Location = new Point(10, 102), Size = new Size(390, 23), PlaceholderText = "03:00,09:00,15:00,21:00", Enabled = false };
        grpSchedule.Controls.AddRange([rdoFixedTimes, txtFixedTimes]);

        AddLabel(grpSchedule, "Warning (minutes before):", 10, 136);
        numWarningMins = new NumericUpDown { Location = new Point(200, 133), Size = new Size(60, 23), Minimum = 1, Maximum = 60, Value = 10 };
        grpSchedule.Controls.Add(numWarningMins);

        AddLabel(grpSchedule, "Warning message:", 10, 166);
        txtRestartMessage = new TextBox { Location = new Point(10, 184), Size = new Size(390, 23), Text = "Server restarting in {minutes} minutes!" };
        grpSchedule.Controls.Add(txtRestartMessage);

        lblNextRestart2 = new Label { Text = "Next restart: ---", Location = new Point(10, 218), AutoSize = true, Tag = "sub" };
        grpSchedule.Controls.Add(lblNextRestart2);

        // ── Auto Backup ──────────────────────────────────────────────
        grpAutoBackup = MakeGroupBox("Auto Backup", 10, 272, 420, 160);

        chkAutoBackup = new CheckBox { Text = "Enable Auto Backup", Location = new Point(10, 24), AutoSize = true, Font = new Font("Segoe UI", 9f, FontStyle.Bold) };
        grpAutoBackup.Controls.Add(chkAutoBackup);

        AddLabel(grpAutoBackup, "Backup every:", 10, 54);
        cmbBackupInterval = new ComboBox { Location = new Point(130, 51), Size = new Size(120, 23), DropDownStyle = ComboBoxStyle.DropDownList };
        cmbBackupInterval.Items.AddRange(["Every 1 hour", "Every 2 hours", "Every 4 hours", "Every 6 hours", "Every 12 hours", "Every 24 hours"]);
        cmbBackupInterval.SelectedIndex = 3;
        grpAutoBackup.Controls.Add(cmbBackupInterval);

        AddLabel(grpAutoBackup, "Keep last:", 10, 84);
        numBackupKeep = new NumericUpDown { Location = new Point(130, 81), Size = new Size(70, 23), Minimum = 1, Maximum = 100, Value = 10 };
        AddLabel(grpAutoBackup, "backups", 210, 84);
        grpAutoBackup.Controls.Add(numBackupKeep);

        btnCreateBackupNow2 = new Button { Text = "Create Backup Now", Location = new Point(10, 110), Size = new Size(160, 32), FlatStyle = FlatStyle.Flat, Tag = "accent" };
        grpAutoBackup.Controls.Add(btnCreateBackupNow2);

        lblNextBackup2 = new Label { Text = "Next backup: ---", Location = new Point(180, 118), AutoSize = true, Tag = "sub" };
        grpAutoBackup.Controls.Add(lblNextBackup2);

        // ── Discord Webhook ──────────────────────────────────────────
        grpDiscord = MakeGroupBox("Discord Webhook Notifications", 444, 10, 480, 250);

        chkDiscordEnabled = new CheckBox { Text = "Enable Discord Notifications", Location = new Point(10, 24), AutoSize = true, Font = new Font("Segoe UI", 9f, FontStyle.Bold) };
        grpDiscord.Controls.Add(chkDiscordEnabled);

        AddLabel(grpDiscord, "Webhook URL:", 10, 54);
        txtWebhookUrl = new TextBox { Location = new Point(10, 72), Size = new Size(450, 23), PlaceholderText = "https://discord.com/api/webhooks/..." };
        grpDiscord.Controls.Add(txtWebhookUrl);

        btnTestWebhook = new Button { Text = "Test Webhook", Location = new Point(10, 104), Size = new Size(120, 30), FlatStyle = FlatStyle.Flat };
        grpDiscord.Controls.Add(btnTestWebhook);

        AddLabel(grpDiscord, "Notify on:", 10, 146);
        chkNotifyStart   = new CheckBox { Text = "Server Start",   Location = new Point(10, 166), AutoSize = true, Checked = true };
        chkNotifyStop    = new CheckBox { Text = "Server Stop",    Location = new Point(140, 166), AutoSize = true, Checked = true };
        chkNotifyCrash   = new CheckBox { Text = "Crash",          Location = new Point(260, 166), AutoSize = true, Checked = true };
        chkNotifyRestart = new CheckBox { Text = "Scheduled Restart", Location = new Point(10, 192), AutoSize = true, Checked = true };
        chkNotifyBackup  = new CheckBox { Text = "Backup Created", Location = new Point(140, 192), AutoSize = true };
        grpDiscord.Controls.AddRange([chkNotifyStart, chkNotifyStop, chkNotifyCrash,
            chkNotifyRestart, chkNotifyBackup]);

        // Crash Detection sub-group in Discord box
        var grpCrash = MakeGroupBox("Crash Detection & Auto-Restart", 444, 272, 480, 160);
        var chkCrashEnabled = new CheckBox { Text = "Enable Crash Detection", Location = new Point(10, 24), AutoSize = true, Font = new Font("Segoe UI", 9f, FontStyle.Bold), Name = "chkCrashDetection", Checked = true };
        var chkCrashRestart = new CheckBox { Text = "Auto-Restart on Crash",  Location = new Point(10, 50), AutoSize = true, Name = "chkAutoRestart", Checked = true };
        AddLabel(grpCrash, "Max restart attempts:", 10, 80);
        var numMaxAttempts = new NumericUpDown { Location = new Point(170, 77), Size = new Size(60, 23), Minimum = 1, Maximum = 10, Value = 3, Name = "numMaxRestartAttempts" };
        grpCrash.Controls.AddRange([chkCrashEnabled, chkCrashRestart, numMaxAttempts]);

        pnlAuto.Controls.AddRange([grpSchedule, grpAutoBackup, grpDiscord, grpCrash]);
        tabAutomation.Controls.Add(pnlAuto);
    }

    // ═════════════════════════════════════════════════════════════════
    // Backups tab
    // ═════════════════════════════════════════════════════════════════
    private void BuildBackupsTab()
    {
        var pnlBackupTop = new Panel { Dock = DockStyle.Top, Height = 46, Padding = new Padding(8, 6, 8, 6) };

        btnCreateBackup      = new Button { Text = "Create Backup",    Size = new Size(130, 30), Location = new Point(8,   7), FlatStyle = FlatStyle.Flat, Tag = "accent" };
        btnRestoreBackup     = new Button { Text = "Restore Selected", Size = new Size(130, 30), Location = new Point(146, 7), FlatStyle = FlatStyle.Flat, Enabled = false };
        btnDeleteBackup      = new Button { Text = "Delete Selected",  Size = new Size(130, 30), Location = new Point(284, 7), FlatStyle = FlatStyle.Flat, Enabled = false, Tag = "danger" };
        btnOpenBackupsFolder = new Button { Text = "Open Folder",      Size = new Size(110, 30), Location = new Point(422, 7), FlatStyle = FlatStyle.Flat };

        pnlBackupTop.Controls.AddRange([btnCreateBackup, btnRestoreBackup, btnDeleteBackup, btnOpenBackupsFolder]);

        dgvBackups = new DataGridView
        {
            Dock                        = DockStyle.Fill,
            AllowUserToAddRows          = false,
            AllowUserToDeleteRows       = false,
            ReadOnly                    = true,
            SelectionMode               = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect                 = false,
            RowHeadersVisible           = false,
            AutoSizeColumnsMode         = DataGridViewAutoSizeColumnsMode.Fill,
            BorderStyle                 = BorderStyle.None,
            ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
            ColumnHeadersHeight         = 30,
            AllowUserToResizeRows       = false
        };
        dgvBackups.Columns.Add(new DataGridViewTextBoxColumn { Name = "Date",     HeaderText = "Date",      FillWeight = 22 });
        dgvBackups.Columns.Add(new DataGridViewTextBoxColumn { Name = "Size",     HeaderText = "Size",      FillWeight = 10 });
        dgvBackups.Columns.Add(new DataGridViewTextBoxColumn { Name = "FileName", HeaderText = "File Name", FillWeight = 68 });

        tabBackups.Controls.Add(dgvBackups);
        tabBackups.Controls.Add(pnlBackupTop);
    }

    // ═════════════════════════════════════════════════════════════════
    // About tab
    // ═════════════════════════════════════════════════════════════════
    private void BuildAboutTab()
    {
        var tcAbout = new DarkTabControl { Dock = DockStyle.Fill };
        var tpAbout    = new TabPage("About");
        var tpCluster  = new TabPage("Cluster Guide");
        var tpCommands = new TabPage("Console Commands");
        tcAbout.TabPages.AddRange([tpAbout, tpCluster, tpCommands]);
        tabAbout.Controls.Add(tcAbout);

        BuildAboutSubTab(tpAbout);
        BuildClusterGuideTab(tpCluster);
        BuildConsoleCommandsTab(tpCommands);
    }

    private void BuildAboutSubTab(TabPage parent)
    {
        var pnlAbout = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(30, 20, 30, 20) };

        // ── Title ────────────────────────────────────────────────────
        lblAboutTitle   = new Label { Text = "SoulMask Server Manager",               Font = new Font("Segoe UI", 20f, FontStyle.Bold), AutoSize = true, Location = new Point(30, 20) };
        lblAboutVersion = new Label { Text = $"Version {Application.ProductVersion}", Font = new Font("Segoe UI", 10f), AutoSize = true, Location = new Point(30, 62), Tag = "sub" };
        lblAboutDotNet  = new Label { Text = $"Running on .NET {Environment.Version}", Font = new Font("Segoe UI", 9f), AutoSize = true, Location = new Point(30, 112), Tag = "sub" };
        lblAboutOs      = new Label { Text = $"OS: {Environment.OSVersion}",           Font = new Font("Segoe UI", 9f), AutoSize = true, Location = new Point(30, 134), Tag = "sub" };
        lblAboutCpu     = new Label { Text = $"CPU Cores: {Environment.ProcessorCount}", Font = new Font("Segoe UI", 9f), AutoSize = true, Location = new Point(30, 156), Tag = "sub" };

        // ── Launcher Requirements ────────────────────────────────────
        var grpReq = new GroupBox { Text = "Launcher Requirements", Location = new Point(30, 190), Size = new Size(680, 100) };
        var lblReq1 = new Label { Text = "Requires .NET 10 Desktop Runtime (x64). Most Windows 10/11 systems already have it.", Font = new Font("Segoe UI", 9.5f, FontStyle.Bold), AutoSize = true, Location = new Point(12, 26) };
        var lblReq2 = new Label { Text = "If the launcher fails to start, download the runtime from Microsoft (free, ~55 MB):", Font = new Font("Segoe UI", 9f), AutoSize = true, Location = new Point(12, 50), Tag = "sub" };
        var lnkRuntime = new LinkLabel { Text = "https://dotnet.microsoft.com/download/dotnet/10.0", Font = new Font("Segoe UI", 9f), AutoSize = true, Location = new Point(12, 72), LinkColor = Color.FromArgb(97, 218, 251), ActiveLinkColor = Color.White, VisitedLinkColor = Color.FromArgb(97, 218, 251) };
        lnkRuntime.Click += (_, _) => System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("https://dotnet.microsoft.com/download/dotnet/10.0") { UseShellExecute = true });
        grpReq.Controls.AddRange([lblReq1, lblReq2, lnkRuntime]);

        // ── Windows Server Prerequisites ─────────────────────────────
        var grpServer = new GroupBox { Text = "Windows Server Prerequisites (run in PowerShell as Administrator)", Location = new Point(30, 305), Size = new Size(720, 360) };

        // 1. Visual C++ — most likely fix for DirectX error on Server 2022
        var lblVc = new Label { Text = "1. Visual C++ Redistributable 2015–2022 (x64) — most common fix for the DirectX error (run in PowerShell as Administrator):", Font = new Font("Segoe UI", 9f, FontStyle.Bold), AutoSize = true, Location = new Point(12, 26) };
        var lblVcStep1 = new Label { Text = "Step 1 — Download:", Font = new Font("Segoe UI", 9f), AutoSize = true, Location = new Point(12, 50), Tag = "sub" };
        var txtVc = new TextBox { Text = @"Invoke-WebRequest -Uri ""https://aka.ms/vs/17/release/vc_redist.x64.exe"" -OutFile ""C:\vc_redist.x64.exe""", ReadOnly = true, Font = new Font("Cascadia Mono", 8.5f), Location = new Point(120, 47), Width = 520, BackColor = Color.FromArgb(30, 30, 30), ForeColor = Color.FromArgb(204, 204, 204), BorderStyle = BorderStyle.FixedSingle };
        var btnCopyVc = new Button { Text = "Copy", Size = new Size(64, 24), Location = new Point(648, 46), FlatStyle = FlatStyle.Flat, Tag = "accent" };
        btnCopyVc.Click += async (_, _) => { Clipboard.SetText(txtVc.Text); btnCopyVc.Text = "✓"; await Task.Delay(1500); btnCopyVc.Text = "Copy"; };
        var lblVcStep2 = new Label { Text = "Step 2 — Install:", Font = new Font("Segoe UI", 9f), AutoSize = true, Location = new Point(12, 76), Tag = "sub" };
        var txtVc2 = new TextBox { Text = @"Start-Process ""C:\vc_redist.x64.exe"" -ArgumentList ""/install /quiet /norestart"" -Wait", ReadOnly = true, Font = new Font("Cascadia Mono", 8.5f), Location = new Point(120, 73), Width = 520, BackColor = Color.FromArgb(30, 30, 30), ForeColor = Color.FromArgb(204, 204, 204), BorderStyle = BorderStyle.FixedSingle };
        var btnCopyVc2 = new Button { Text = "Copy", Size = new Size(64, 24), Location = new Point(648, 72), FlatStyle = FlatStyle.Flat, Tag = "accent" };
        btnCopyVc2.Click += async (_, _) => { Clipboard.SetText(txtVc2.Text); btnCopyVc2.Text = "✓"; await Task.Delay(1500); btnCopyVc2.Text = "Copy"; };

        // 2. Desktop Experience (Server 2016/2019) or Server-Gui-Shell (Server 2022)
        var lblDx = new Label { Text = "2. Desktop Experience / DirectX (if VC++ alone doesn't fix it):", Font = new Font("Segoe UI", 9f, FontStyle.Bold), AutoSize = true, Location = new Point(12, 104) };
        var lblDxNote = new Label { Text = "Server 2016/2019:", Font = new Font("Segoe UI", 9f), AutoSize = true, Location = new Point(12, 126), Tag = "sub" };
        var txtDx = new TextBox { Text = "Install-WindowsFeature -Name Desktop-Experience -Restart", ReadOnly = true, Font = new Font("Cascadia Mono", 9f), Location = new Point(120, 123), Width = 520, BackColor = Color.FromArgb(30, 30, 30), ForeColor = Color.FromArgb(204, 204, 204), BorderStyle = BorderStyle.FixedSingle };
        var btnCopyDx = new Button { Text = "Copy", Size = new Size(64, 24), Location = new Point(648, 122), FlatStyle = FlatStyle.Flat, Tag = "accent" };
        btnCopyDx.Click += async (_, _) => { Clipboard.SetText(txtDx.Text); btnCopyDx.Text = "✓"; await Task.Delay(1500); btnCopyDx.Text = "Copy"; };

        var lblDxNote2 = new Label { Text = "Server 2022:", Font = new Font("Segoe UI", 9f), AutoSize = true, Location = new Point(12, 153), Tag = "sub" };
        var txtDx2 = new TextBox { Text = "Install-WindowsFeature -Name Server-Gui-Shell -Restart", ReadOnly = true, Font = new Font("Cascadia Mono", 9f), Location = new Point(120, 150), Width = 520, BackColor = Color.FromArgb(30, 30, 30), ForeColor = Color.FromArgb(204, 204, 204), BorderStyle = BorderStyle.FixedSingle };
        var btnCopyDx2 = new Button { Text = "Copy", Size = new Size(64, 24), Location = new Point(648, 149), FlatStyle = FlatStyle.Flat, Tag = "accent" };
        btnCopyDx2.Click += async (_, _) => { Clipboard.SetText(txtDx2.Text); btnCopyDx2.Text = "✓"; await Task.Delay(1500); btnCopyDx2.Text = "Copy"; };

        var lblReboot = new Label { Text = "⚠  Reboot required after Desktop Experience / Server-Gui-Shell install.", Font = new Font("Segoe UI", 9f, FontStyle.Italic), AutoSize = true, Location = new Point(12, 183), ForeColor = Color.FromArgb(255, 152, 0) };

        // 3. Firewall ports
        var lblPorts = new Label { Text = "3. Open Firewall Ports (required for players to connect):", Font = new Font("Segoe UI", 9f, FontStyle.Bold), AutoSize = true, Location = new Point(12, 210) };
        var lblPortsUdp = new Label { Text = "UDP (game + query):", Font = new Font("Segoe UI", 9f), AutoSize = true, Location = new Point(12, 232), Tag = "sub" };
        var txtPortsUdp = new TextBox { Text = "New-NetFirewallRule -DisplayName \"SoulMask UDP\" -Direction Inbound -Protocol UDP -LocalPort 8777,27015 -Action Allow", ReadOnly = true, Font = new Font("Cascadia Mono", 8.5f), Location = new Point(140, 229), Width = 500, BackColor = Color.FromArgb(30, 30, 30), ForeColor = Color.FromArgb(204, 204, 204), BorderStyle = BorderStyle.FixedSingle };
        var btnCopyPortsUdp = new Button { Text = "Copy", Size = new Size(64, 24), Location = new Point(648, 228), FlatStyle = FlatStyle.Flat, Tag = "accent" };
        btnCopyPortsUdp.Click += async (_, _) => { Clipboard.SetText(txtPortsUdp.Text); btnCopyPortsUdp.Text = "✓"; await Task.Delay(1500); btnCopyPortsUdp.Text = "Copy"; };

        var lblPortsTcp = new Label { Text = "TCP (EchoPort):", Font = new Font("Segoe UI", 9f), AutoSize = true, Location = new Point(12, 262), Tag = "sub" };
        var txtPortsTcp = new TextBox { Text = "New-NetFirewallRule -DisplayName \"SoulMask TCP\" -Direction Inbound -Protocol TCP -LocalPort 18888 -Action Allow", ReadOnly = true, Font = new Font("Cascadia Mono", 8.5f), Location = new Point(140, 259), Width = 500, BackColor = Color.FromArgb(30, 30, 30), ForeColor = Color.FromArgb(204, 204, 204), BorderStyle = BorderStyle.FixedSingle };
        var btnCopyPortsTcp = new Button { Text = "Copy", Size = new Size(64, 24), Location = new Point(648, 258), FlatStyle = FlatStyle.Flat, Tag = "accent" };
        btnCopyPortsTcp.Click += async (_, _) => { Clipboard.SetText(txtPortsTcp.Text); btnCopyPortsTcp.Text = "✓"; await Task.Delay(1500); btnCopyPortsTcp.Text = "Copy"; };

        var lblPortsRcon = new Label { Text = "TCP (RCON port 19000) — only needed if you enable RCON in Settings:", Font = new Font("Segoe UI", 9f), AutoSize = true, Location = new Point(12, 292), Tag = "sub" };
        var txtPortsRcon = new TextBox { Text = "New-NetFirewallRule -DisplayName \"SoulMask RCON\" -Direction Inbound -Protocol TCP -LocalPort 19000 -Action Allow", ReadOnly = true, Font = new Font("Cascadia Mono", 8.5f), Location = new Point(12, 309), Width = 628, BackColor = Color.FromArgb(30, 30, 30), ForeColor = Color.FromArgb(204, 204, 204), BorderStyle = BorderStyle.FixedSingle };
        var btnCopyPortsRcon = new Button { Text = "Copy", Size = new Size(64, 24), Location = new Point(648, 308), FlatStyle = FlatStyle.Flat, Tag = "accent" };
        btnCopyPortsRcon.Click += async (_, _) => { Clipboard.SetText(txtPortsRcon.Text); btnCopyPortsRcon.Text = "✓"; await Task.Delay(1500); btnCopyPortsRcon.Text = "Copy"; };

        grpServer.Controls.AddRange([lblVc, lblVcStep1, txtVc, btnCopyVc, lblVcStep2, txtVc2, btnCopyVc2,
            lblDx, lblDxNote, txtDx, btnCopyDx, lblDxNote2, txtDx2, btnCopyDx2, lblReboot,
            lblPorts, lblPortsUdp, txtPortsUdp, btnCopyPortsUdp,
            lblPortsTcp, txtPortsTcp, btnCopyPortsTcp,
            lblPortsRcon, txtPortsRcon, btnCopyPortsRcon]);

        // ── Info footer ──────────────────────────────────────────────
        var lblNote = new Label
        {
            Text     = "Steam App ID (server): 3017310  |  Game App ID: 2646460  |  Launcher: ServerFiles\\WSServer.exe",
            Font     = new Font("Segoe UI", 9f),
            AutoSize = true,
            Location = new Point(30, 540),
            Tag      = "sub"
        };

        pnlAbout.Controls.AddRange([lblAboutTitle, lblAboutVersion, lblAboutDotNet,
            lblAboutOs, lblAboutCpu, grpReq, grpServer, lblNote]);
        parent.Controls.Add(pnlAbout);
    }

    private static void BuildClusterGuideTab(TabPage parent)
    {
        var pnl = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(20, 16, 20, 20) };

        // ── Header ───────────────────────────────────────────────────
        var lblTitle = new Label
        {
            Text     = "Cluster Setup Guide",
            Font     = new Font("Segoe UI", 16f, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(20, 16)
        };
        var lblSub = new Label
        {
            Text     = "A SoulMask cluster links multiple servers so players can travel between maps. Manage all instances from a single exe using the tabs above.",
            Font     = new Font("Segoe UI", 9f),
            AutoSize = true,
            Location = new Point(20, 52),
            Tag      = "sub"
        };
        var lnkVideo = new LinkLabel
        {
            Text             = "▶  Video Tutorial: How to Setup a SoulMask Cluster",
            Font             = new Font("Segoe UI", 9.5f),
            AutoSize         = true,
            Location         = new Point(20, 76),
            LinkColor        = Color.FromArgb(97, 218, 251),
            ActiveLinkColor  = Color.White,
            VisitedLinkColor = Color.FromArgb(97, 218, 251)
        };
        lnkVideo.Click += (_, _) => System.Diagnostics.Process.Start(
            new System.Diagnostics.ProcessStartInfo("https://youtu.be/UgVNxni_STM") { UseShellExecute = true });

        // Helper: renders each step as a RichTextBox inside a GroupBox for clean spacing + colored warnings
        static GroupBox MakeStep(string title, string[] lines, int y)
        {
            // 10pt Segoe UI in a RichTextBox renders at ~20px per line; blank lines count as ~10px
            int contentH = lines.Sum(l => l == "" ? 10 : 20);
            int rtbH     = contentH + 6;
            int grpH     = rtbH + 44; // GroupBox title bar (~20) + top/bottom padding (~24)

            var rtb = new RichTextBox
            {
                ReadOnly    = true,
                BorderStyle = BorderStyle.None,
                BackColor   = Color.FromArgb(37, 37, 38),
                ForeColor   = Color.FromArgb(204, 204, 204),
                Font        = new Font("Segoe UI", 10f),
                ScrollBars  = RichTextBoxScrollBars.None,
                WordWrap    = true,
                DetectUrls  = false,
                Location    = new Point(10, 22),
                Size        = new Size(834, rtbH),
            };

            foreach (var line in lines)
            {
                if (line.StartsWith("⚠"))
                {
                    rtb.SelectionColor = Color.FromArgb(255, 152, 0);
                    rtb.SelectionFont  = new Font("Segoe UI", 10f, FontStyle.Bold);
                }
                else
                {
                    rtb.SelectionColor = Color.FromArgb(204, 204, 204);
                    rtb.SelectionFont  = new Font("Segoe UI", 10f);
                }
                rtb.AppendText(line + "\n");
            }

            var grp = new GroupBox
            {
                Text     = title,
                Font     = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                Location = new Point(20, y),
                Size     = new Size(860, grpH),
            };
            grp.Controls.Add(rtb);
            return grp;
        }

        // Calculate y positions dynamically so adding/removing lines never clips
        string[][] stepLines =
        [
            [   // Step 1 — Install
                "• A single SoulMaskServerManager.exe manages all server instances from one window.",
                "• Each instance gets its own folder under SoulMaskServer\\ next to the exe.",
                "    Example:  SoulMaskServer\\Server 1\\  and  SoulMaskServer\\Server 2\\",
                "",
                "• Use  File → Add Server Instance  to create additional instances at any time.",
                "• Select each instance tab and install via  Dashboard → Install Server.",
                "",
                "⚠  Open firewall ports for every instance — each uses a different port set (see About → Prerequisites)."
            ],
            [   // Step 2 — Configure
                "• On the Main Server tab:  Settings → Cluster → Role = Main Server,  Server ID = 1.",
                "• Set Main Server Port (default 18888) — clients will connect back to this port.",
                "• Enable  KaiQiKuaFu (Cross-server Mode)  in the Gameplay tab, then Save.",
                "",
                "• Click  Add Client Server  — one button automatically:",
                "    — Creates a new instance with ports offset by +1  (Game: 8778  Query: 27016  Echo: 18889)",
                "    — Sets Role = Client Server,  Server ID = 2,  and points it at the Main Server.",
                "    — Enables KaiQiKuaFu on the client.",
                "",
                "• Review the client's Settings tab and confirm the Main Server IP is correct.",
                "⚠  Add firewall rules for the client instance's ports too (see About → Prerequisites)."
            ],
            [   // Step 3 — Start, Stop & Player Travel
                "• START ORDER:  Main Server first — wait for Dashboard to show  Running,  then start Client(s).",
                "• STOP ORDER:   Stop all Client Servers first, then stop the Main Server.",
                "⚠  Stopping the Main while clients are running may cause the Main Server to hang.",
                "",
                "• Existing single-server save?  Run  Migrate Save  on the Main Server before the first cluster start.",
                "    This copies world.db → account.db via CopyRoles.exe.  Back up your save first.",
                "",
                "Player Travel  (KaiQiKuaFu must be enabled on ALL servers in the cluster):",
                "• Players visit the Mysterious Island in the ocean on either map.",
                "• Interact with the terminal in front of the portal and select the destination server.",
                "• Only non-initial tribesmen can travel — the starting character cannot use the portal.",
                "• All servers should share the same password for seamless player transfers."
            ]
        ];

        string[] stepTitles =
        [
            "Step 1 — Install Server Instances",
            "Step 2 — Configure & Add Client Server",
            "Step 3 — Start, Stop & Player Travel"
        ];

        int nextY = 108;
        var groups = new List<GroupBox>();
        for (int i = 0; i < stepTitles.Length; i++)
        {
            var grp = MakeStep(stepTitles[i], stepLines[i], nextY);
            groups.Add(grp);
            nextY += grp.Height + 12;
        }

        pnl.Controls.Add(lblTitle);
        pnl.Controls.Add(lblSub);
        pnl.Controls.Add(lnkVideo);
        foreach (var g in groups) pnl.Controls.Add(g);
        parent.Controls.Add(pnl);
    }

    private static void BuildConsoleCommandsTab(TabPage parent)
    {
        // Header label (Top)
        var pnlTop = new Panel { Dock = DockStyle.Top, Height = 54 };
        var lblTitle = new Label
        {
            Text     = "Console Commands Reference",
            Font     = new Font("Segoe UI", 14f, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(12, 8)
        };
        var lblSub = new Label
        {
            Text     = "Commands are sent over EchoPort (loopback TCP — Dashboard → Console). " +
                       "Aliases shown in parentheses work too.",
            Font     = new Font("Segoe UI", 9f),
            AutoSize = true,
            Location = new Point(12, 34),
            Tag      = "sub"
        };
        pnlTop.Controls.AddRange([lblTitle, lblSub]);

        // DataGridView (Fill — added first)
        var dgv = new DataGridView
        {
            Dock                  = DockStyle.Fill,
            ReadOnly              = true,
            AllowUserToAddRows    = false,
            AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false,
            RowHeadersVisible     = false,
            SelectionMode         = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect           = false,
            AutoSizeRowsMode      = DataGridViewAutoSizeRowsMode.AllCells,
            ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
            ColumnHeadersHeight   = 28,
            Font                  = new Font("Segoe UI", 9f),
            CellBorderStyle       = DataGridViewCellBorderStyle.SingleHorizontal,
            GridColor             = Color.FromArgb(60, 60, 60)
        };
        dgv.DefaultCellStyle.WrapMode = DataGridViewTriState.True;
        dgv.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Command",     Width = 220, DefaultCellStyle = { Font = new Font("Cascadia Mono", 8.5f) } });
        dgv.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Description", FillWeight = 100, AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
        dgv.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Category",    Width = 110 });

        // Commands table
        (string cmd, string desc, string cat)[] rows =
        [
            // Players
            ("lp",                    "List online players. Returns a pipe-delimited table: SteamID | PlayerName | PawnID | Position.\n(alias: List_OnlinePlayers)",                                                        "Players"),
            ("lap",                   "List ALL players who have ever logged in, including offline.\n(alias: List_AllPlayers)",                                                                                              "Players"),
            ("say <message>",         "Broadcast a message to system chat. Shows as \"System: <msg>\" in chat.\nNote: this is a chat message, not a screen popup.",                                                        "Players"),
            ("kick <steamID>",        "Kick a player from the server (UE console command).\nNote: works via EchoPort on most versions.",                                                                                   "Players"),
            ("usp 1 1 <steamID>",     "Ban a player (adds to BlackAccountList.txt).\n(alias: Update_ServerPermissionList)",                                                                                                "Players"),
            ("usp 1 0 <steamID>",     "Unban a player.\n(alias: Update_ServerPermissionList)",                                                                                                                            "Players"),
            ("usp 4 1 <steamID>",     "Mute a player (adds to BanSpeek.txt).\n(alias: Update_ServerPermissionList)",                                                                                                      "Players"),
            ("usp 4 0 <steamID>",     "Unmute a player.\n(alias: Update_ServerPermissionList)",                                                                                                                           "Players"),
            ("lsp",                   "List the server permission list (bans, mutes, admins).\n(alias: List_ServerPermissionList)",                                                                                        "Players"),
            ("fly <steamID> <1|0>",   "Enable (1) or disable (0) fly mode for a player.\n(alias: FlyMode)",                                                                                                               "Players"),
            ("go <steamID> X Y Z",    "Teleport a player to map coordinates X Y Z.\n(alias: GotoPostion)",                                                                                                                "Players"),
            // Server
            ("shutdown <seconds>",    "Graceful shutdown: warns players with an on-screen countdown.\nPassing 0 sets a 300-second timer. Run SaveWorld 0 first.\n(alias: SaveAndExit)",                                   "Server"),
            ("cc",                    "Cancel a pending shutdown timer.\nNote: the on-screen countdown display is not removed.\n(alias: StopCloseServer)",                                                                 "Server"),
            ("fps",                   "Report current server FPS / tick rate.\n(alias: ServerFPS)",                                                                                                                        "Server"),
            ("qi",                    "Get the current server invitation code.\n(alias: QueryInvitationCode)",                                                                                                             "Server"),
            // World / Save
            ("SaveWorld 0",           "Save world state to memory.\nRun this before bkh or bk.\nNote: SaveWorld 1 is broken — do not use.",                                                                               "World"),
            ("bkh",                   "Save a timestamped backup to disk (BackupByHour).\nAlways run SaveWorld 0 first.\n(alias: BackupDatabaseByHour)",                                                                  "World"),
            ("bk <name>",             "Save a named backup to disk.\nAlways run SaveWorld 0 first.\n(alias: BackupDatabase)",                                                                                              "World"),
            // Gameplay
            ("lc [name]",             "List all gameplay coefficient settings, or filter by name.\n(alias: Show_Coefficient_Settings)",                                                                                    "Gameplay"),
            ("sc <key> <value>",      "Set a gameplay coefficient live without restarting.\nExample: sc PlayerDamageRateToMonster 2.0\n(alias: Set_Coefficient)",                                                         "Gameplay"),
        ];

        foreach (var (cmd, desc, cat) in rows)
            dgv.Rows.Add(cmd, desc, cat);

        // Docking: Fill first, Top second
        parent.Controls.Add(dgv);
        parent.Controls.Add(pnlTop);
    }

    // ═════════════════════════════════════════════════════════════════
    // Helper builders
    // ═════════════════════════════════════════════════════════════════
    private static GroupBox MakeGroupBox(string text, int x, int y, int w, int h)
    {
        var gb = new GroupBox { Text = text, Location = new Point(x, y), Size = new Size(w, h) };
        gb.Font = new Font("Segoe UI", 9f, FontStyle.Regular);
        return gb;
    }

    private static Label AddLabel(Control parent, string text, int x, int y, string? tag = null)
    {
        var lbl = new Label { Text = text, AutoSize = true, Location = new Point(x, y + 3) };
        if (tag != null) lbl.Tag = tag;
        parent.Controls.Add(lbl);
        return lbl;
    }

    private static TextBox AddTextBox(Control parent, int x, int y, int w, string placeholder)
    {
        var tb = new TextBox { Location = new Point(x, y), Size = new Size(w, 23), PlaceholderText = placeholder };
        parent.Controls.Add(tb);
        return tb;
    }

    private static NumericUpDown AddNumeric(Control parent, int x, int y,
        decimal min, decimal max, decimal val)
    {
        var n = new NumericUpDown { Location = new Point(x, y), Size = new Size(90, 23), Minimum = min, Maximum = max, Value = val };
        parent.Controls.Add(n);
        return n;
    }

    private static Button MakeSmallButton(string text, int x, int y)
    {
        return new Button
        {
            Text      = text,
            Size      = new Size(text.Length < 6 ? 55 : 120, 26),
            Location  = new Point(x, y),
            FlatStyle = FlatStyle.Flat
        };
    }
}
