using System.Diagnostics;
using SoulMaskServerManager.Models;

namespace SoulMaskServerManager.Forms;

public partial class MainForm : Form
{
    // ── Paths ────────────────────────────────────────────────────────
    private readonly string RootDir;
    private readonly string _instanceName;
    private readonly bool   _isEmbedded;

    // ── Services ─────────────────────────────────────────────────────
    private readonly FileLogger              _logger;
    private readonly SteamCmdService         _steamCmd;
    private readonly ServerManager           _serverManager;
    private readonly ConfigurationManager    _configManager;
    private readonly BackupService           _backupService;
    private readonly RconClient              _rconClient;
    private readonly ScheduledRestartService _scheduleService;
    private readonly DiscordWebhookService   _discordService;

    // ── State ─────────────────────────────────────────────────────────
    private ServerConfiguration _config = new();
    private bool _isDirty;
    private DateTime _serverStartTime;
    private CancellationTokenSource? _installCts;

    // ── Timers ────────────────────────────────────────────────────────
    private readonly System.Windows.Forms.Timer _uptimeTimer = new() { Interval = 1000 };
    private readonly System.Windows.Forms.Timer _playerTimer = new() { Interval = 30_000 };

    // ── Cluster manager integration ───────────────────────────────────
    /// <summary>Fired whenever this instance's server state changes. Used by ClusterManagerForm to update its status strip and tab labels.</summary>
    public event EventHandler<ServerState>? InstanceStateChanged;
    public string InstanceName    => _instanceName;
    public string ServerName      => _config.ServerName;
    public bool   IsServerRunning => _serverManager.State is ServerState.Running or ServerState.Starting;
    public Models.ClusterRole ClusterRole  => _config.ClusterRole;
    public Models.ServerConfiguration CurrentConfig => _config;

    // ── Constructor ───────────────────────────────────────────────────
    public MainForm(string rootDir, string instanceName, bool embedded = false)
    {
        RootDir        = rootDir;
        _instanceName  = instanceName;
        _isEmbedded    = embedded;

        // Ensure directory structure exists
        Directory.CreateDirectory(RootDir);
        Directory.CreateDirectory(Path.Combine(RootDir, "steamcmd"));
        Directory.CreateDirectory(Path.Combine(RootDir, "Backups"));
        Directory.CreateDirectory(Path.Combine(RootDir, "Logs"));

        // Create services
        _logger          = new FileLogger(Path.Combine(RootDir, "Logs"));
        _configManager   = new ConfigurationManager(RootDir, _logger);
        _steamCmd        = new SteamCmdService(RootDir, _logger);
        _serverManager   = new ServerManager(RootDir, _logger);
        _backupService   = new BackupService(RootDir, _logger);
        _rconClient      = new RconClient(_logger);
        _scheduleService = new ScheduledRestartService(_logger);
        _discordService  = new DiscordWebhookService(_logger);

        InitializeComponent();

        // When embedded in ClusterManagerForm, hide chrome handled by the outer form
        if (_isEmbedded)
        {
            menuStrip.Visible   = false;
            statusStrip.Visible = false;
        }

        // Load saved config
        _config = _configManager.LoadSettings();

        // Build dynamic CPU core checkboxes
        BuildCpuCoreCheckboxes();

        // Populate all UI from config
        LoadSettingsIntoUi();

        // Wire up all events
        WireEvents();

        // Apply saved theme
        ThemeManager.Apply(this, _config.Theme);
        if (!_isEmbedded) UpdateThemeMenuChecks();

        // Windows re-applies dark mode theming when the form is first shown,
        // overriding our SetWindowTheme call in the constructor. Re-apply it
        // in the Shown event (fires AFTER Windows finishes theme propagation).
        this.Shown += (_, _) => ThemeManager.ReapplyConsoleThemeOverrides(this);

        // Set initial server state (syncs _serverManager.State so button click logic is correct)
        _serverManager.InitializeState();
        UpdateServerState(_serverManager.State);

        // Start timers
        _uptimeTimer.Tick += UptimeTick;
        _uptimeTimer.Start();

        _logger.Info($"Instance '{_instanceName}' started.");
    }

    // Backward-compat standalone constructor (used when not running under ClusterManagerForm)
    public MainForm() : this(
        Path.Combine(AppContext.BaseDirectory, "SoulMaskServer"),
        "Server", embedded: false) { }

    /// <summary>Graceful shutdown called by ClusterManagerForm before closing.</summary>
    public async Task ShutdownAsync()
    {
        if (_serverManager.IsRunning)
            await _serverManager.StopAsync(_rconClient, _config);

        BuildConfigFromUi();
        _configManager.SaveSettings(_config);
        _uptimeTimer.Stop();
        _playerTimer.Stop();
        _scheduleService.Dispose();
        _backupService.Dispose();
        _logger.Info($"Instance '{_instanceName}' shut down.");
    }

    // ── Wiring ────────────────────────────────────────────────────────
    private void WireEvents()
    {
        // Form — embedded instances are shut down by ClusterManagerForm
        if (!_isEmbedded) FormClosing += OnFormClosing;

        // Menu
        menuOpenServerFolder.Click += (_, _) => OpenFolder(RootDir);
        menuOpenBackupFolder.Click += (_, _) => OpenFolder(_backupService.BackupsDirectory);
        menuOpenGameIni.Click      += (_, _) => OpenFile(_configManager.GameIniPath);
        menuOpenEngineIni.Click    += (_, _) => OpenFile(_configManager.EngineIniPath);
        menuExit.Click             += (_, _) => Close();
        menuThemeDark.Click        += (_, _) => ApplyTheme(AppTheme.Dark);
        menuThemeLight.Click       += (_, _) => ApplyTheme(AppTheme.Light);

        // Dashboard
        btnServerAction.Click += BtnServerAction_Click;
        btnUpdate.Click        += BtnUpdate_Click;
        btnRestart.Click       += BtnRestart_Click;
        btnClearConsole.Click  += (_, _) => rtbConsole.Clear();

        // Settings
        btnSaveSettings.Click   += BtnSaveSettings_Click;
        btnReloadSettings.Click += (_, _) => { LoadSettingsIntoUi(); SetDirty(false); };
        numEchoPort.ValueChanged += (_, _) => SetDirty(true);
        cmbClusterRole.SelectedIndexChanged += (_, _) => { UpdateClusterVisibility(); SetDirty(true); };
        btnMigrateSave.Click        += (_, _) => RunMigrateSave();
        rdoInterval.CheckedChanged  += RdoRestartMode_Changed;
        rdoFixedTimes.CheckedChanged += RdoRestartMode_Changed;

        // Mark dirty on any settings change
        foreach (Control c in GetAllSettingsControls())
        {
            if (c is TextBox tb)         tb.TextChanged     += (_, _) => SetDirty(true);
            if (c is NumericUpDown nud)  nud.ValueChanged   += (_, _) => SetDirty(true);
            if (c is ComboBox cmb)       cmb.SelectedIndexChanged += (_, _) => SetDirty(true);
            if (c is CheckBox chk)       chk.CheckedChanged += (_, _) => SetDirty(true);
        }

        // Config editor
        btnSaveGameIni.Click    += (_, _) => _configManager.WriteGameIni(rtbGameIni.Text);
        btnReloadGameIni.Click  += (_, _) => rtbGameIni.Text = _configManager.ReadGameIni();
        btnOpenGameIni.Click    += (_, _) => OpenFile(_configManager.GameIniPath);
        btnSaveEngineIni.Click  += (_, _) => _configManager.WriteEngineIni(rtbEngineIni.Text);
        btnReloadEngineIni.Click+= (_, _) => rtbEngineIni.Text = _configManager.ReadEngineIni();
        btnOpenEngineIni.Click  += (_, _) => OpenFile(_configManager.EngineIniPath);

        // Gameplay settings
        btnSaveGameplay.Click            += (_, _) => SaveGameplaySettings();
        btnReloadGameplay.Click          += (_, _) => LoadGameplaySettings();
        btnSaveAsPreset.Click            += (_, _) => SaveAsNewPreset();
        btnDeletePreset.Click            += (_, _) => DeleteCurrentCustomPreset();
        btnResetGameplayDefaults.Click   += (_, _) => ResetGameplayToDefaults();
        btnApplyLive.Click        += async (_, _) => await ApplyGameplaySettingLiveAsync();
        cmbGameplayPreset.SelectedIndexChanged += (_, _) =>
        {
            if (_gameplayDirty)
            {
                if (MessageBox.Show(
                    "You have unsaved changes in the current preset.\nSwitch anyway and lose changes?",
                    "Unsaved Changes", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                {
                    RefreshPresetComboBox();
                    return;
                }
            }
            PopulateGameplayGrid();
            UpdatePresetButtons();

            // Persist the selected preset so it survives a restart
            if (cmbGameplayPreset.SelectedItem?.ToString() is string sel)
            {
                _config.LastGameplayPreset = sel;
                _configManager.SaveSettings(_config);
            }
        };
        txtGameplaySearch.TextChanged          += (_, _) => ApplyGameplayFilter(txtGameplaySearch.Text);
        dgvGameplay.CellValueChanged  += DgvGameplay_CellValueChanged;
        dgvGameplay.CellBeginEdit     += DgvGameplay_CellBeginEdit;
        dgvGameplay.SelectionChanged  += DgvGameplay_SelectionChanged;
        tabMain.SelectedIndexChanged  += async (_, _) =>
        {
            if (tabMain.SelectedTab == tabGameplay && _gameplayPresets.Count == 0)
                LoadGameplaySettings();
            else if (tabMain.SelectedTab == tabMods)
                LoadModsTab();
            else if (tabMain.SelectedTab == tabBackups)
                RefreshBackupList();
            else if (tabMain.SelectedTab == tabPlayers && _serverManager.IsRunning)
                await RefreshPlayersAsync();
        };

        // Mods
        btnAddMod.Click           += OnAddMod;
        btnRemoveMod.Click        += OnRemoveMod;
        btnMoveModUp.Click        += (_, _) => MoveSelectedMod(-1);
        btnMoveModDown.Click      += (_, _) => MoveSelectedMod(+1);
        btnOpenWorkshop.Click     += OnOpenWorkshop;
        btnSaveMods.Click         += OnSaveMods;
        btnCheckModUpdates.Click  += async (_, _) => await CheckModUpdatesAsync();
        btnUpdateMods.Click       += async (_, _) => await OnUpdateModsAsync();
        txtModInput.KeyDown       += (_, e) => { if (e.KeyCode == Keys.Enter) { OnAddMod(null, EventArgs.Empty); e.SuppressKeyPress = true; } };
        lvMods.ItemDrag           += (_, e) => lvMods.DoDragDrop(e.Item!, DragDropEffects.Move);
        lvMods.DragEnter          += (_, e) => e.Effect = e.Data?.GetDataPresent(typeof(ListViewItem)) == true ? DragDropEffects.Move : DragDropEffects.None;
        lvMods.DragDrop           += OnModDragDrop;

        // Players
        btnRefreshPlayers.Click  += async (_, _) => await RefreshPlayersAsync();
        btnKickPlayer.Click      += async (_, _) => await KickSelectedPlayerAsync();
        btnBanPlayer.Click       += async (_, _) => await BanSelectedPlayerAsync();
        btnMutePlayer.Click      += async (_, _) => await MuteSelectedPlayerAsync();
        btnMessagePlayer.Click   += async (_, _) => await MessageSelectedPlayerAsync();
        btnBanList.Click         += async (_, _) => await ShowBanListAsync();
        btnBroadcast.Click       += async (_, _) => await BroadcastAsync();
        btnClearRconOutput.Click += (_, _) => rtbRconOutput.Clear();
        dgvPlayers.SelectionChanged   += DgvPlayers_SelectionChanged;
        chkAutoRefreshPlayers.CheckedChanged += (_, _) => SetAutoRefresh(chkAutoRefreshPlayers.Checked);
        InitPlayerRefreshTimer();

        // Automation
        btnTestWebhook.Click         += async (_, _) => await TestWebhookAsync();
        btnCreateBackupNow2.Click    += async (_, _) => await CreateBackupAsync();

        // Backups
        btnCreateBackup.Click      += async (_, _) => await CreateBackupAsync();
        btnRestoreBackup.Click     += async (_, _) => await RestoreBackupAsync();
        btnDeleteBackup.Click      += (_, _) => DeleteSelectedBackup();
        btnOpenBackupsFolder.Click += (_, _) => OpenFolder(_backupService.BackupsDirectory);
        dgvBackups.SelectionChanged += DgvBackups_SelectionChanged;

        // Server manager events
        _serverManager.StateChanged   += OnServerStateChanged;
        _serverManager.OutputReceived += OnServerOutput;
        _serverManager.CrashDetected  += OnCrashDetected;
        _serverManager.AutoRestarted  += OnAutoRestarted;

        // SteamCMD events
        _steamCmd.OutputReceived         += OnSteamCmdOutput;
        _steamCmd.DownloadProgressChanged += OnDownloadProgress;

        // Backup events
        _backupService.BackupCreated += (_, path) =>
            this.InvokeIfRequired(() => {
                AppendConsole($"Backup created: {Path.GetFileName(path)}", Color.FromArgb(156, 110, 201));
                RefreshBackupList();
                if (_config.EnableDiscordWebhook && _config.NotifyOnBackup)
                    _ = _discordService.NotifyBackup(_config.DiscordWebhookUrl, _config.ServerName, path);
            });

        // Schedule events
        _scheduleService.WarningIssued += async (_, mins) =>
        {
            string msg = _config.RestartWarningMessage.Replace("{minutes}", mins.ToString());
            AppendConsole($"[Schedule] Warning sent: {msg}", Color.FromArgb(255, 193, 7));
            if (_serverManager.IsRunning)
                await _rconClient.BroadcastAsync("127.0.0.1", _config.EchoPort, msg);
        };

        _scheduleService.RestartTriggered += async (_, _) =>
        {
            AppendConsole("[Schedule] Performing scheduled restart...", Color.FromArgb(255, 152, 0));
            if (_config.EnableDiscordWebhook && _config.NotifyOnRestart)
                await _discordService.NotifyRestart(_config.DiscordWebhookUrl, _config.ServerName, "Scheduled restart");
            if (_serverManager.IsRunning)
                await _serverManager.RestartAsync(_rconClient, _config);
        };

        // Logger events — show in log tab
        _logger.LogWritten += (_, e) =>
            this.InvokeIfRequired(() => UpdateLogTab(e.Level, e.Message));
    }

    // ── Theme ─────────────────────────────────────────────────────────
    private void ApplyTheme(AppTheme theme)
    {
        _config.Theme = theme;
        _configManager.SaveSettings(_config);

        var result = MessageBox.Show(
            "Theme change requires a restart to apply fully.\nRestart now?",
            "Restart Required",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (result == DialogResult.Yes)
            Application.Restart();
    }

    private void UpdateThemeMenuChecks()
    {
        menuThemeDark.Checked  = _config.Theme == AppTheme.Dark;
        menuThemeLight.Checked = _config.Theme == AppTheme.Light;
    }

    // ── Server State ─────────────────────────────────────────────────
    private void UpdateServerState(ServerState state)
    {
        this.InvokeIfRequired(() =>
        {
            (string label, Color dot, string btnText) = state switch
            {
                ServerState.NotInstalled => ("Not Installed", Color.Gray,                    "Install Server"),
                ServerState.Installing   => ("Installing...", ThemeManager.StateInstalling,  "Cancel"),
                ServerState.Stopped      => ("Stopped",       ThemeManager.StateStopped,     "Start Server"),
                ServerState.Starting     => ("Starting...",   ThemeManager.StateStarting,    "Stop Server"),
                ServerState.Running      => ("Running",       ThemeManager.StateRunning,     "Stop Server"),
                ServerState.Stopping     => ("Stopping...",   ThemeManager.StateStarting,    "..."),
                ServerState.Crashed      => ("Crashed",       ThemeManager.StateCrashed,     "Start Server"),
                _                        => ("Unknown",       Color.Gray,                    "?")
            };

            lblStatusDot.ForeColor = dot;
            lblStatus.Text         = label;
            tsStatus.Text          = $"Status: {label}";

            btnServerAction.Text    = btnText;
            btnUpdate.Enabled       = state is ServerState.Stopped or ServerState.NotInstalled or ServerState.Crashed;
            btnRestart.Enabled      = state == ServerState.Running;
            progressBar.Visible     = state == ServerState.Installing;
            lblProgress.Visible     = state == ServerState.Installing;

            if (!_isEmbedded)
            {
                Text = state == ServerState.Running
                    ? $"SoulMask Server Manager  v{Application.ProductVersion}  —  {_config.ServerName}"
                    : $"SoulMask Server Manager  v{Application.ProductVersion}";
            }

            if (state == ServerState.Running)
                _serverStartTime = DateTime.Now;

            if (state != ServerState.Running)
            {
                tsUptime.Text     = "Uptime: --:--:--";
                tsPlayers.Text    = "Players: 0";
                lblUptime2.Text   = "Uptime: --:--:--";
                lblPlayerCount2.Text = "Players: 0";
            }
        });
    }

    private void OnServerStateChanged(object? sender, ServerState state)
    {
        UpdateServerState(state);
        InstanceStateChanged?.Invoke(this, state);
    }

    // ── Console Output ────────────────────────────────────────────────
    public void AppendConsole(string message, Color? color = null)
    {
        this.InvokeIfRequired(() =>
        {
            bool scroll = chkAutoScroll.Checked;
            rtbConsole.AppendConsoleLine(message,
                color ?? WinFormsExtensions.ClassifyConsoleLine(message), scroll);
        });
    }

    private void OnServerOutput(object? sender, string msg) => AppendConsole(msg);

    private void OnSteamCmdOutput(object? sender, string msg) =>
        AppendConsole(msg, WinFormsExtensions.ClassifyConsoleLine(msg));

    private void OnDownloadProgress(object? sender, int pct) =>
        this.InvokeIfRequired(() =>
        {
            progressBar.Value  = Math.Clamp(pct, 0, 100);
            lblProgress.Text   = $"Downloading... {pct}%";
        });

    // ── Settings persistence ──────────────────────────────────────────
    public void LoadSettingsIntoUi()
    {
        txtServerName.Text    = _config.ServerName;
        numMaxPlayers.Value   = _config.MaxPlayers;
        txtAdminPassword.Text = _config.AdminPassword;
        txtServerPassword.Text= _config.ServerPassword;
        numGamePort.Value         = _config.GamePort;
        numQueryPort.Value        = _config.QueryPort;
        numEchoPort.Value         = _config.EchoPort;
        numServerPermMask.Value   = _config.ServerPermissionMask;
        numSaveInterval.Value = _config.SaveInterval;
        chkRconEnabled.Checked  = _config.RconEnabled;
        txtRconPassword.Text    = _config.RconPassword;
        numRconPort.Value       = _config.RconPort;
        txtRconAddress.Text     = _config.RconAddress;
        chkPveMode.Checked    = _config.PveMode;
        txtCustomArgs.Text    = _config.CustomLaunchArgs;
        cmbClusterRole.SelectedIndex  = (int)_config.ClusterRole;
        numClusterId.Value            = _config.ClusterId;
        numClusterMainPort.Value      = _config.ClusterMainPort;
        txtClusterClientConnect.Text  = _config.ClusterClientConnect;
        UpdateClusterVisibility();

        int mapIdx = cmbMap.Items.IndexOf(_config.MapName);
        cmbMap.SelectedIndex = mapIdx >= 0 ? mapIdx : 0;

        int priIdx = cmbProcessPriority.Items.IndexOf(_config.ProcessPriority);
        cmbProcessPriority.SelectedIndex = priIdx >= 0 ? priIdx : 0;

        chkUseAllCores.Checked = _config.UseAllCores;

        // Automation – Schedule
        chkScheduleEnabled.Checked = _config.ScheduledRestartEnabled;
        rdoInterval.Checked    = !_config.UseFixedRestartTimes;
        rdoFixedTimes.Checked  =  _config.UseFixedRestartTimes;
        numIntervalHours.Value = _config.RestartIntervalHours;
        txtFixedTimes.Text     = _config.FixedRestartTimes;
        numWarningMins.Value   = _config.RestartWarningMinutes;
        txtRestartMessage.Text = _config.RestartWarningMessage;

        // Automation – Backup
        chkAutoBackup.Checked      = _config.AutoBackupEnabled;
        int[] intervalMap          = [1, 2, 4, 6, 12, 24];
        int bi = Array.IndexOf(intervalMap, _config.BackupIntervalHours);
        cmbBackupInterval.SelectedIndex = bi >= 0 ? bi : 3;
        numBackupKeep.Value        = _config.BackupKeepCount;

        // Automation – Discord
        chkDiscordEnabled.Checked = _config.EnableDiscordWebhook;
        txtWebhookUrl.Text        = _config.DiscordWebhookUrl;
        chkNotifyStart.Checked    = _config.NotifyOnStart;
        chkNotifyStop.Checked     = _config.NotifyOnStop;
        chkNotifyCrash.Checked    = _config.NotifyOnCrash;
        chkNotifyRestart.Checked  = _config.NotifyOnRestart;
        chkNotifyBackup.Checked   = _config.NotifyOnBackup;

        // Crash detection
        FindControl<CheckBox>("chkCrashDetection").Do(c => c.Checked = _config.EnableCrashDetection);
        FindControl<CheckBox>("chkAutoRestart").Do(c => c.Checked    = _config.AutoRestart);
        FindControl<NumericUpDown>("numMaxRestartAttempts").Do(c => c.Value = _config.MaxRestartAttempts);

        // Config editor
        rtbGameIni.Text   = _configManager.ReadGameIni();
        rtbEngineIni.Text = _configManager.ReadEngineIni();

        // Apply schedule & backup services
        ApplyScheduleFromUi();
        ApplyBackupFromUi();
    }

    private void BuildConfigFromUi()
    {
        _config.ServerName         = txtServerName.Text.Trim();
        _config.MaxPlayers         = (int)numMaxPlayers.Value;
        _config.AdminPassword      = txtAdminPassword.Text;
        _config.ServerPassword     = txtServerPassword.Text;
        _config.GamePort              = (int)numGamePort.Value;
        _config.QueryPort             = (int)numQueryPort.Value;
        _config.EchoPort              = (int)numEchoPort.Value;
        _config.ServerPermissionMask  = (int)numServerPermMask.Value;
        _config.SaveInterval       = (int)numSaveInterval.Value;
        _config.RconEnabled        = chkRconEnabled.Checked;
        _config.RconPassword       = txtRconPassword.Text;
        _config.RconPort           = (int)numRconPort.Value;
        _config.RconAddress        = string.IsNullOrWhiteSpace(txtRconAddress.Text) ? "0.0.0.0" : txtRconAddress.Text.Trim();
        _config.PveMode            = chkPveMode.Checked;
        _config.CustomLaunchArgs   = txtCustomArgs.Text.Trim();
        _config.ClusterRole        = (ClusterRole)cmbClusterRole.SelectedIndex;
        _config.ClusterId          = (int)numClusterId.Value;
        _config.ClusterMainPort    = (int)numClusterMainPort.Value;
        _config.ClusterClientConnect = txtClusterClientConnect.Text.Trim();
        _config.MapName            = cmbMap.SelectedItem?.ToString() ?? "Level01_Main";
        _config.ProcessPriority    = cmbProcessPriority.SelectedItem?.ToString() ?? "Normal";
        _config.UseAllCores        = chkUseAllCores.Checked;
        _config.CpuAffinity        = BuildCpuAffinityMask();

        _config.ScheduledRestartEnabled = chkScheduleEnabled.Checked;
        _config.UseFixedRestartTimes    = rdoFixedTimes.Checked;
        _config.RestartIntervalHours    = (int)numIntervalHours.Value;
        _config.FixedRestartTimes       = txtFixedTimes.Text.Trim();
        _config.RestartWarningMinutes   = (int)numWarningMins.Value;
        _config.RestartWarningMessage   = txtRestartMessage.Text;

        _config.AutoBackupEnabled    = chkAutoBackup.Checked;
        int[] imap = [1, 2, 4, 6, 12, 24];
        _config.BackupIntervalHours  = imap[Math.Clamp(cmbBackupInterval.SelectedIndex, 0, imap.Length - 1)];
        _config.BackupKeepCount      = (int)numBackupKeep.Value;

        _config.EnableDiscordWebhook = chkDiscordEnabled.Checked;
        _config.DiscordWebhookUrl    = txtWebhookUrl.Text.Trim();
        _config.NotifyOnStart        = chkNotifyStart.Checked;
        _config.NotifyOnStop         = chkNotifyStop.Checked;
        _config.NotifyOnCrash        = chkNotifyCrash.Checked;
        _config.NotifyOnRestart      = chkNotifyRestart.Checked;
        _config.NotifyOnBackup       = chkNotifyBackup.Checked;

        FindControl<CheckBox>("chkCrashDetection").Do(c => _config.EnableCrashDetection = c.Checked);
        FindControl<CheckBox>("chkAutoRestart").Do(c    => _config.AutoRestart = c.Checked);
        FindControl<NumericUpDown>("numMaxRestartAttempts").Do(c => _config.MaxRestartAttempts = (int)c.Value);
    }

    // ── Dirty Tracking ────────────────────────────────────────────────
    private void SetDirty(bool dirty)
    {
        _isDirty = dirty;
        lblDirtyIndicator.Visible = dirty;
    }

    // ── CPU Core Checkboxes ───────────────────────────────────────────
    private void BuildCpuCoreCheckboxes()
    {
        flpCpuCores.Controls.Clear();
        for (int i = 0; i < Environment.ProcessorCount; i++)
        {
            var cb = new CheckBox
            {
                Text    = $"C{i}",
                Checked = true,
                AutoSize = true,
                Margin  = new Padding(2, 0, 2, 0),
                Tag     = i
            };
            cb.CheckedChanged += (_, _) => SetDirty(true);
            flpCpuCores.Controls.Add(cb);
        }
    }

    private string BuildCpuAffinityMask()
    {
        if (chkUseAllCores.Checked) return "";
        long mask = 0;
        foreach (CheckBox cb in flpCpuCores.Controls.OfType<CheckBox>())
        {
            if (cb.Checked && cb.Tag is int idx)
                mask |= 1L << idx;
        }
        return mask == 0 ? "" : mask.ToString();
    }

    // ── Uptime Timer ──────────────────────────────────────────────────
    private void UptimeTick(object? sender, EventArgs e)
    {
        if (_serverManager.State == ServerState.Running)
        {
            var up = DateTime.Now - _serverStartTime;
            string s = $"{(int)up.TotalHours:D2}:{up.Minutes:D2}:{up.Seconds:D2}";
            tsUptime.Text        = $"Uptime: {s}";
            lblUptime2.Text      = $"Uptime: {s}";
        }

        // Update next restart/backup labels
        if (_scheduleService.NextRestart != DateTime.MaxValue)
        {
            tsNextRestart.Text = $"Next Restart: {_scheduleService.NextRestart:HH:mm}";
            var diff = _scheduleService.NextRestart - DateTime.Now;
            lblNextRestart2.Text = $"Next restart: {_scheduleService.NextRestart:HH:mm} (in {(int)diff.TotalHours}h {diff.Minutes}m)";
        }

        if (_backupService.NextBackupTime != DateTime.MaxValue)
        {
            tsNextBackup.Text  = $"Next Backup: {_backupService.NextBackupTime:HH:mm}";
            var diff = _backupService.NextBackupTime - DateTime.Now;
            lblNextBackup2.Text = $"Next backup: {_backupService.NextBackupTime:HH:mm} (in {(int)diff.TotalHours}h {diff.Minutes}m)";
        }
    }

    // ── Crash / Auto-Restart Notifications ───────────────────────────
    private void OnCrashDetected(object? sender, EventArgs e)
    {
        AppendConsole("[CRASH] Server crash detected!", ThemeManager.StateCrashed);
        if (_config.EnableDiscordWebhook && _config.NotifyOnCrash)
            _ = _discordService.NotifyCrash(_config.DiscordWebhookUrl, _config.ServerName);
    }

    private void OnAutoRestarted(object? sender, EventArgs e) =>
        AppendConsole("[AUTO-RESTART] Server has been automatically restarted.", ThemeManager.StateStarting);

    // ── Log tab ───────────────────────────────────────────────────────
    private void UpdateLogTab(LogLevel level, string message)
    {
        // Nothing yet — placeholder for a future log RichTextBox in About/Logs tab
    }

    // ── Form Closing ──────────────────────────────────────────────────
    private async void OnFormClosing(object? sender, FormClosingEventArgs e)
    {
        _logger.Info($"Form closing — reason: {e.CloseReason}");

        if (_serverManager.IsRunning)
        {
            var result = MessageBox.Show(
                "The server is currently running. Stop it before closing?",
                "Server Running", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Warning);

            if (result == DialogResult.Cancel) { e.Cancel = true; return; }
            if (result == DialogResult.Yes)    await _serverManager.StopAsync(_rconClient, _config);
        }

        // Save current config (including theme)
        BuildConfigFromUi();
        _configManager.SaveSettings(_config);

        _uptimeTimer.Stop();
        _playerTimer.Stop();
        _scheduleService.Dispose();
        _backupService.Dispose();
        _logger.Info("Application closed.");
    }

    // ── Helpers ───────────────────────────────────────────────────────
    private static void OpenFolder(string path)
    {
        Directory.CreateDirectory(path);
        Process.Start("explorer.exe", path);
    }

    private static void OpenFile(string path)
    {
        if (!File.Exists(path))
        {
            MessageBox.Show($"File not found:\n{path}\n\nRun the server at least once to generate config files.",
                "File Not Found", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
    }

    private IEnumerable<Control> GetAllSettingsControls()
    {
        return new Control[]
        {
            txtServerName, txtAdminPassword, txtServerPassword, txtCustomArgs,
            numMaxPlayers, numGamePort, numQueryPort, numEchoPort, numSaveInterval,
            numRconPort, txtRconPassword, txtRconAddress, numServerPermMask,
            chkPveMode, chkUseAllCores, chkRconEnabled,
            cmbMap, cmbProcessPriority,
            numIntervalHours, txtFixedTimes, numWarningMins, txtRestartMessage,
            txtWebhookUrl, numBackupKeep, cmbBackupInterval,
            chkScheduleEnabled, chkAutoBackup, chkDiscordEnabled,
            cmbClusterRole, numClusterId, numClusterMainPort, txtClusterClientConnect
        };
    }

    private T? FindControl<T>(string name) where T : Control
    {
        return Controls.Find(name, searchAllChildren: true).OfType<T>().FirstOrDefault();
    }

    // ── Cluster helpers ───────────────────────────────────────────────

    private void UpdateClusterVisibility()
    {
        var role = (ClusterRole)cmbClusterRole.SelectedIndex;
        bool isMain    = role == ClusterRole.MainServer;
        bool isClient  = role == ClusterRole.ClientServer;
        bool isCluster = isMain || isClient;

        lblClusterRoleDesc.Text = role switch
        {
            ClusterRole.MainServer   => "Adds: -serverid=N -mainserverport=PORT",
            ClusterRole.ClientServer => "Adds: -serverid=N -clientserverconnect=ip:port",
            _                        => "Standalone = no cluster args added to launch command"
        };

        // Server ID row — shown for any cluster role
        numClusterId.Visible     = isCluster;
        lblClusterIdRow.Visible  = isCluster;
        lblClusterIdDesc.Visible = isCluster;

        // Main Server row — broadcast port
        numClusterMainPort.Visible    = isMain;
        lblClusterMainPortRow.Visible = isMain;
        lblClusterMainPortDesc.Visible= isMain;

        // Client Server row — main server address
        txtClusterClientConnect.Visible = isClient;
        lblClusterClientRow.Visible     = isClient;
        lblClusterClientDesc.Visible    = isClient;

        // Migrate Save + Export Client Config + Add Client Server — only relevant for main server
        btnMigrateSave.Visible          = isMain;
        lblMigrateDesc.Visible          = isMain;
    }

    private void RunMigrateSave()
    {
        string wsDir = Path.Combine(RootDir, "ServerFiles", "WS");
        string copyRolesExe = Path.Combine(wsDir, "Plugins", "DBAgent", "ThirdParty", "Binaries", "CopyRoles.exe");

        if (!File.Exists(copyRolesExe))
        {
            MessageBox.Show(
                $"CopyRoles.exe not found at:\n{copyRolesExe}\n\nMake sure the server is installed.",
                "Tool Not Found", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        string map = _config.MapName.Trim();
        if (string.IsNullOrWhiteSpace(map)) map = "Level01_Main";

        string defaultSrc = Path.Combine(wsDir, "Saved", "Worlds", "Dedicated", map, "world.db");
        string defaultDst = Path.Combine(wsDir, "Saved", "Accounts", "account.db");

        MessageBox.Show(
            $"This will copy player account data from:\n{defaultSrc}\n\nTo:\n{defaultDst}\n\n" +
            "IMPORTANT: Back up your save files before proceeding.\n" +
            "The server must be stopped.",
            "Migrate Save — Read This First", MessageBoxButtons.OK, MessageBoxIcon.Information);

        if (MessageBox.Show("Proceed with migration?", "Confirm",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;

        if (_serverManager.IsRunning)
        {
            MessageBox.Show("Stop the server before migrating.", "Server Running",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(defaultDst)!);
            var psi = new System.Diagnostics.ProcessStartInfo(copyRolesExe,
                $"-src=\"{defaultSrc}\" -dst=\"{defaultDst}\"")
            {
                UseShellExecute  = false,
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
                CreateNoWindow   = true
            };
            using var proc = System.Diagnostics.Process.Start(psi)!;
            string output = proc.StandardOutput.ReadToEnd();
            string errors = proc.StandardError.ReadToEnd();
            proc.WaitForExit();

            string result = string.IsNullOrWhiteSpace(errors) ? output : $"{output}\n{errors}";
            if (proc.ExitCode == 0)
            {
                AppendConsole($"[Cluster] Migration complete → {defaultDst}", Color.FromArgb(78, 201, 176));
                MessageBox.Show($"Migration successful!\n\n{result}", "Done",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                AppendConsole($"[Cluster] Migration failed (exit {proc.ExitCode}): {errors}", ThemeManager.StateStopped);
                MessageBox.Show($"Migration failed (exit code {proc.ExitCode}):\n{result}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to run CopyRoles.exe:\n{ex.Message}", "Error",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

}

// Tiny extension to call action on nullable
file static class NullableExt
{
    public static void Do<T>(this T? obj, Action<T> action) where T : class
    {
        if (obj != null) action(obj);
    }
}
