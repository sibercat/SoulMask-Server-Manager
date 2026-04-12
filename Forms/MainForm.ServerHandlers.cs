namespace SoulMaskServerManager.Forms;

partial class MainForm
{
    private async void BtnServerAction_Click(object? sender, EventArgs e)
    {
        switch (_serverManager.State)
        {
            case ServerState.NotInstalled:
            case ServerState.Crashed:
                await InstallServerAsync(validate: false);
                break;

            case ServerState.Stopped:
                StartServer();
                break;

            case ServerState.Running:
            case ServerState.Starting:
                await StopServerAsync();
                break;

            case ServerState.Installing:
                _installCts?.Cancel();
                break;
        }
    }

    private async void BtnUpdate_Click(object? sender, EventArgs e)
    {
        if (_serverManager.IsRunning)
        {
            MessageBox.Show("Stop the server before updating.", "Server Running",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        await InstallServerAsync(validate: true);
    }

    private async void BtnRestart_Click(object? sender, EventArgs e)
    {
        if (MessageBox.Show("Restart the server now?", "Confirm Restart",
            MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;

        AppendConsole("[UI] Restarting server...", ThemeManager.StateStarting);
        await _serverManager.RestartAsync(_rconClient, _config);
    }

    // ── Install / Update ─────────────────────────────────────────────
    private async Task InstallServerAsync(bool validate)
    {
        // If this is a fresh install (not validate/update), check if a sibling instance
        // already has server files we can copy instead of downloading 5+ GB again.
        string? copySource = null;
        if (!validate)
            copySource = FindInstalledSiblingServerFiles();

        if (copySource != null)
        {
            string siblingName = Path.GetFileName(Path.GetDirectoryName(Path.GetDirectoryName(copySource))!);
            var choice = MessageBox.Show(
                $"'{siblingName}' already has server files installed.\n\n" +
                $"Copy files from it instead of re-downloading (~5 GB)?\n\n" +
                $"• Yes — fast local copy (seconds/minutes)\n" +
                $"• No  — download fresh via SteamCMD",
                "Copy Existing Files?", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);

            if (choice == DialogResult.Cancel) return;
            if (choice == DialogResult.No) copySource = null;
        }

        _installCts = new CancellationTokenSource();
        UpdateServerState(ServerState.Installing);
        progressBar.Visible = true;
        progressBar.Value   = 0;
        lblProgress.Visible = true;

        try
        {
            if (copySource != null)
            {
                AppendConsole($"Copying server files from existing instance...", ThemeManager.StateInstalling);
                await _steamCmd.CopyFromInstanceAsync(copySource, _installCts.Token);
            }
            else
            {
                AppendConsole(validate ? "Validating/updating server files..." : "Starting server installation...",
                    ThemeManager.StateInstalling);
                await _steamCmd.InstallOrUpdateServerAsync(validate, _installCts.Token);
            }

            progressBar.Value  = 100;
            lblProgress.Text   = "Complete!";
            AppendConsole("Installation complete!", ThemeManager.StateRunning);

            _serverManager.InitializeState();
            UpdateServerState(_serverManager.State);
        }
        catch (OperationCanceledException)
        {
            AppendConsole("Installation cancelled.", Color.Gray);
            UpdateServerState(ServerState.NotInstalled);
        }
        catch (Exception ex)
        {
            AppendConsole($"Installation failed: {ex.Message}", ThemeManager.StateCrashed);
            _logger.Error("Installation failed.", ex);
            _serverManager.InitializeState();
            UpdateServerState(_serverManager.State);
        }
        finally
        {
            progressBar.Visible = false;
            lblProgress.Visible = false;
            _installCts = null;
        }
    }

    /// <summary>
    /// Scans sibling instance directories (same parent as RootDir) for an already-installed
    /// server and returns the first ServerFiles path found, or null if none exists.
    /// </summary>
    private string? FindInstalledSiblingServerFiles()
    {
        try
        {
            string? parent = Path.GetDirectoryName(RootDir);
            if (parent == null) return null;

            foreach (var dir in Directory.GetDirectories(parent).OrderBy(d => d))
            {
                if (string.Equals(dir, RootDir, StringComparison.OrdinalIgnoreCase)) continue;

                string candidate = Path.Combine(dir, "ServerFiles", "WSServer.exe");
                if (File.Exists(candidate))
                    return Path.GetDirectoryName(candidate)!; // returns the ServerFiles dir
            }
        }
        catch { /* non-fatal */ }
        return null;
    }

    // ── Start ─────────────────────────────────────────────────────────
    private void StartServer()
    {
        if (!_steamCmd.IsServerInstalled)
        {
            MessageBox.Show("Server files are not installed. Use 'Install Server' first.",
                "Not Installed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        // Cluster startup order warning
        if (_config.ClusterRole == Models.ClusterRole.ClientServer)
        {
            if (MessageBox.Show(
                "This server is configured as a Cluster Client.\n\n" +
                "START ORDER: Make sure the Main Server is already running before starting this one.\n" +
                "Client servers will not accept player connections until the Main Server is online.\n\n" +
                "STOP ORDER: Always stop Client Servers before stopping the Main Server.\n" +
                "Stopping Main while clients are connected may cause it to hang.\n\n" +
                "Continue?",
                "Cluster Start Order", MessageBoxButtons.YesNo, MessageBoxIcon.Information) != DialogResult.Yes)
                return;
        }

        // Save current settings before starting
        BuildConfigFromUi();
        _configManager.SaveSettings(_config);

        // Write Game.ini from config
        _configManager.WriteGameIni(_configManager.GenerateDefaultGameIni(_config));

        _serverManager.ConfigureCrashDetection(
            _config.EnableCrashDetection,
            _config.AutoRestart,
            _config.MaxRestartAttempts);

        AppendConsole("[UI] Starting server...", ThemeManager.StateStarting);
        _serverManager.Start(_config);

        if (_config.EnableDiscordWebhook && _config.NotifyOnStart)
            _ = _discordService.NotifyServerStarted(_config.DiscordWebhookUrl, _config.ServerName);

        ApplyScheduleFromUi();
        ApplyBackupFromUi();
    }

    // ── Stop ──────────────────────────────────────────────────────────
    private async Task StopServerAsync()
    {
        AppendConsole("[UI] Stopping server...", ThemeManager.StateStopped);
        await _serverManager.StopAsync(_rconClient, _config);

        if (_config.EnableDiscordWebhook && _config.NotifyOnStop)
            await _discordService.NotifyServerStopped(_config.DiscordWebhookUrl, _config.ServerName);
    }

    // ── Save Settings ─────────────────────────────────────────────────
    private void BtnSaveSettings_Click(object? sender, EventArgs e)
    {
        BuildConfigFromUi();
        _configManager.SaveSettings(_config);
        SetDirty(false);
        AppendConsole("[Settings] Settings saved.", Color.FromArgb(78, 201, 176));

        // Apply services with new config immediately
        _serverManager.ConfigureCrashDetection(
            _config.EnableCrashDetection, _config.AutoRestart, _config.MaxRestartAttempts);
        ApplyScheduleFromUi();
        ApplyBackupFromUi();
    }

    private void ApplyScheduleFromUi()
    {
        int[] intervalMap = [1, 2, 4, 6, 12, 24];
        int backupHours = intervalMap[Math.Clamp(cmbBackupInterval.SelectedIndex, 0, intervalMap.Length - 1)];

        _scheduleService.Configure(
            chkScheduleEnabled.Checked,
            rdoFixedTimes.Checked,
            (int)numIntervalHours.Value,
            txtFixedTimes.Text,
            (int)numWarningMins.Value);
    }

    private void ApplyBackupFromUi()
    {
        int[] intervalMap = [1, 2, 4, 6, 12, 24];
        int hours = intervalMap[Math.Clamp(cmbBackupInterval.SelectedIndex, 0, intervalMap.Length - 1)];
        _backupService.Configure(chkAutoBackup.Checked, hours, (int)numBackupKeep.Value);
    }

    private void RdoRestartMode_Changed(object? sender, EventArgs e)
    {
        txtFixedTimes.Enabled = rdoFixedTimes.Checked;
        numIntervalHours.Enabled = rdoInterval.Checked;
    }
}
