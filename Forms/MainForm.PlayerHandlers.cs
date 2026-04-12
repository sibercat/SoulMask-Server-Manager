using System.Text.Json;

namespace SoulMaskServerManager.Forms;

partial class MainForm
{
    private readonly System.Windows.Forms.Timer _playerRefreshTimer = new() { Interval = 30_000 };

    private void InitPlayerRefreshTimer()
    {
        _playerRefreshTimer.Tick += async (_, _) =>
        {
            // Only refresh when the Players tab is visible and server is running
            if (tabMain.SelectedTab == tabPlayers && _serverManager.IsRunning)
                await RefreshPlayersAsync();
        };
    }

    private void SetAutoRefresh(bool enabled)
    {
        if (enabled)
            _playerRefreshTimer.Start();
        else
            _playerRefreshTimer.Stop();
    }

    private async Task RefreshPlayersAsync()
    {
        lblRconStatus.Text = "EchoPort: Connecting...";
        btnRefreshPlayers.Enabled = false;

        try
        {
            var players = await _rconClient.GetPlayersAsync("127.0.0.1", _config.EchoPort);

            if (players == null)
            {
                lblRconStatus.ForeColor = ThemeManager.StateStopped;
                lblRconStatus.Text      = "EchoPort: Unreachable";
                rtbRconOutput.AppendConsoleLine("EchoPort: Could not connect. Is the server running?", ThemeManager.StateStopped);
                return;
            }

            dgvPlayers.Rows.Clear();
            foreach (var p in players)
                dgvPlayers.Rows.Add(p.Name, p.SteamId);

            tsPlayers.Text          = $"Players: {players.Count}";
            lblPlayerCount2.Text    = $"Players: {players.Count}";
            lblRconStatus.ForeColor = ThemeManager.StateRunning;
            lblRconStatus.Text      = $"EchoPort: Connected  ({players.Count} player{(players.Count != 1 ? "s" : "")})";
            rtbRconOutput.AppendConsoleLine($"Player list refreshed: {players.Count} online.", ThemeManager.StateRunning);
        }
        catch (Exception ex)
        {
            lblRconStatus.ForeColor = ThemeManager.StateStopped;
            lblRconStatus.Text      = "EchoPort: Error";
            rtbRconOutput.AppendConsoleLine($"EchoPort error: {ex.Message}", ThemeManager.StateStopped);
        }
        finally
        {
            btnRefreshPlayers.Enabled = true;
        }
    }

    private async Task KickSelectedPlayerAsync()
    {
        var (name, steamId) = GetSelectedPlayer();
        if (steamId == null) return;

        if (MessageBox.Show($"Kick player '{name}'?", "Confirm Kick",
            MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;

        bool ok = await _rconClient.KickPlayerAsync("127.0.0.1", _config.EchoPort, steamId);
        rtbRconOutput.AppendConsoleLine(
            ok ? $"Kicked: {name}" : $"Kick failed for: {name}",
            ok ? ThemeManager.StateRunning : ThemeManager.StateStopped);

        if (ok) await RefreshPlayersAsync();
    }

    private async Task BanSelectedPlayerAsync()
    {
        var (name, steamId) = GetSelectedPlayer();
        if (steamId == null) return;

        if (MessageBox.Show($"Ban player '{name}'?\n(Adds to server ban list permanently.)", "Confirm Ban",
            MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;

        bool ok = await _rconClient.BanPlayerAsync("127.0.0.1", _config.EchoPort, steamId);
        rtbRconOutput.AppendConsoleLine(
            ok ? $"Banned: {name} ({steamId})" : $"Ban failed for: {name}",
            ok ? ThemeManager.StateRunning : ThemeManager.StateStopped);

        if (ok)
        {
            CachePlayerName(_configManager.BanCachePath, steamId, name ?? steamId);
            await RefreshPlayersAsync();
        }
    }

    // ── Bans & Mutes dialog ───────────────────────────────────────────

    private async Task ShowBanListAsync()
    {
        using var dlg = new Form
        {
            Text            = "Bans & Mutes",
            Size            = new Size(580, 460),
            StartPosition   = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox     = false, MinimizeBox = false
        };

        var tabs     = new TabControl { Location = new Point(12, 12), Size = new Size(540, 360) };
        var tabBans  = new TabPage("  Banned  ");
        var tabMutes = new TabPage("  Muted  ");
        tabs.TabPages.AddRange([tabBans, tabMutes]);

        var btnClose = new Button { Text = "Close", Size = new Size(90, 32), Location = new Point(462, 384), FlatStyle = FlatStyle.Flat, DialogResult = DialogResult.Cancel };
        dlg.Controls.AddRange([tabs, btnClose]);
        dlg.CancelButton = btnClose;

        BuildRestrictionTab(tabBans,
            listFile:    _configManager.BanListPath,
            cachePath:   _configManager.BanCachePath,
            actionLabel: "Unban Selected",
            confirmMsg:  (name) => $"Unban '{name}'?\nThey will be able to rejoin the server.",
            logLabel:    "Unbanned",
            rconAction:  (id) => _rconClient.UnbanPlayerAsync("127.0.0.1", _config.EchoPort, id),
            noun:        "banned");

        BuildRestrictionTab(tabMutes,
            listFile:    _configManager.MuteListPath,
            cachePath:   _configManager.MuteCachePath,
            actionLabel: "Unmute Selected",
            confirmMsg:  (name) => $"Unmute '{name}'?\nThey will be able to send chat messages again.",
            logLabel:    "Unmuted",
            rconAction:  (id) => _rconClient.UnmutePlayerAsync("127.0.0.1", _config.EchoPort, id),
            noun:        "muted");

        ThemeManager.Apply(dlg, ThemeManager.Current);
        dlg.ShowDialog(this);
    }

    private void BuildRestrictionTab(TabPage tab, string listFile, string cachePath,
        string actionLabel, Func<string, string> confirmMsg, string logLabel,
        Func<string, Task<bool>> rconAction, string noun)
    {
        var cache   = LoadCache(cachePath);
        var entries = ReadListFile(listFile);

        var grid = new DataGridView
        {
            Dock                        = DockStyle.Top,
            Height                      = 260,
            AllowUserToAddRows          = false,
            AllowUserToDeleteRows       = false,
            ReadOnly                    = true,
            SelectionMode               = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect                 = false,
            RowHeadersVisible           = false,
            AutoSizeColumnsMode         = DataGridViewAutoSizeColumnsMode.Fill,
            BorderStyle                 = BorderStyle.None,
            ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
            ColumnHeadersHeight         = 30
        };
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Name",    HeaderText = "Player Name", FillWeight = 45 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "SteamId", HeaderText = "Steam ID",    FillWeight = 55 });

        foreach (var id in entries)
        {
            cache.TryGetValue(id, out string? n);
            grid.Rows.Add(n ?? "(unknown)", id);
        }

        var pnlBottom = new Panel { Dock = DockStyle.Fill, Padding = new Padding(4) };
        var lblCount  = new Label  { Text = CountLabel(grid.Rows.Count, noun), AutoSize = true, Location = new Point(4, 10) };
        var btnAction = new Button { Text = actionLabel, Size = new Size(140, 30), Location = new Point(4, 30), FlatStyle = FlatStyle.Flat, Enabled = false, Tag = "accent" };

        grid.SelectionChanged += (_, _) => btnAction.Enabled = grid.SelectedRows.Count > 0;

        btnAction.Click += async (_, _) =>
        {
            if (grid.SelectedRows.Count == 0) return;
            string id   = grid.SelectedRows[0].Cells["SteamId"].Value?.ToString() ?? "";
            string name = grid.SelectedRows[0].Cells["Name"].Value?.ToString() ?? id;

            if (MessageBox.Show(confirmMsg(name), "Confirm", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;

            bool ok = await rconAction(id);
            if (ok)
            {
                RemoveFromCache(cachePath, id);
                grid.Rows.Remove(grid.SelectedRows[0]);
                lblCount.Text = CountLabel(grid.Rows.Count, noun);
                rtbRconOutput.AppendConsoleLine($"{logLabel}: {name} ({id})", ThemeManager.StateRunning);
            }
            else
            {
                MessageBox.Show($"{actionLabel} failed. Is the server running?", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        };

        pnlBottom.Controls.AddRange([lblCount, btnAction]);

        // Fill must be added first (processed last), Top second
        tab.Controls.Add(pnlBottom);
        tab.Controls.Add(grid);
    }

    private static string CountLabel(int count, string noun) =>
        $"{count} {noun} player{(count != 1 ? "s" : "")}";

    // ── Cache helpers ─────────────────────────────────────────────────

    private static Dictionary<string, string> LoadCache(string path)
    {
        try
        {
            if (File.Exists(path))
                return JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(path)) ?? [];
        }
        catch { }
        return [];
    }

    private static void CachePlayerName(string path, string steamId, string name)
    {
        try
        {
            var cache = LoadCache(path);
            cache[steamId] = name;
            File.WriteAllText(path, JsonSerializer.Serialize(cache, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }

    private static void RemoveFromCache(string path, string steamId)
    {
        try
        {
            var cache = LoadCache(path);
            cache.Remove(steamId);
            File.WriteAllText(path, JsonSerializer.Serialize(cache, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }

    private static List<string> ReadListFile(string path)
    {
        try
        {
            if (File.Exists(path))
                return File.ReadAllLines(path)
                           .Select(l => l.Trim())
                           .Where(l => l.Length > 0)
                           .ToList();
        }
        catch { }
        return [];
    }

    private async Task MessageSelectedPlayerAsync()
    {
        var (name, steamId) = GetSelectedPlayer();
        if (steamId == null) return;

        string? msg = ShowInputDialog($"Send message to all players (targeting '{name}'):", "Broadcast Message", "");
        if (string.IsNullOrWhiteSpace(msg)) return;

        // SoulMask EchoPort has no per-player DM; broadcast with name context
        bool ok = await _rconClient.BroadcastAsync("127.0.0.1", _config.EchoPort, $"[MSG to {name}] {msg}");
        rtbRconOutput.AppendConsoleLine(
            ok ? $"Message broadcast for {name}." : "Broadcast failed.",
            ok ? ThemeManager.StateRunning : ThemeManager.StateStopped);
    }

    /// <summary>Simple inline input dialog — avoids Microsoft.VisualBasic dependency.</summary>
    private string? ShowInputDialog(string prompt, string title, string defaultValue = "")
    {
        using var dlg   = new Form { Text = title, Size = new Size(440, 160), StartPosition = FormStartPosition.CenterParent, FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false };
        var lbl  = new Label  { Text = prompt, AutoSize = true, Location = new Point(12, 12) };
        var tb   = new TextBox { Text = defaultValue, Location = new Point(12, lbl.Bottom + 8), Width = 400 };
        var ok   = new Button  { Text = "OK",     DialogResult = DialogResult.OK,     Size = new Size(80, 28), Location = new Point(240, tb.Bottom + 10) };
        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Size = new Size(80, 28), Location = new Point(332, tb.Bottom + 10) };
        dlg.Controls.AddRange([lbl, tb, ok, cancel]);
        dlg.AcceptButton = ok;
        dlg.CancelButton = cancel;
        ThemeManager.Apply(dlg, ThemeManager.Current);
        return dlg.ShowDialog(this) == DialogResult.OK ? tb.Text : null;
    }

    private async Task MuteSelectedPlayerAsync()
    {
        var (name, steamId) = GetSelectedPlayer();
        if (steamId == null) return;

        // Show Mute / Unmute / Cancel dialog
        using var dlg    = new Form { Text = "Mute / Unmute", Size = new Size(340, 130), StartPosition = FormStartPosition.CenterParent, FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false };
        var lbl          = new Label  { Text = $"Select action for '{name}':", AutoSize = true, Location = new Point(12, 16) };
        var btnMute      = new Button { Text = "Mute",   Size = new Size(85, 30), Location = new Point(12, 50),  FlatStyle = FlatStyle.Flat, DialogResult = DialogResult.Yes };
        var btnUnmute    = new Button { Text = "Unmute", Size = new Size(85, 30), Location = new Point(108, 50), FlatStyle = FlatStyle.Flat, DialogResult = DialogResult.No };
        var btnCancel    = new Button { Text = "Cancel", Size = new Size(85, 30), Location = new Point(204, 50), FlatStyle = FlatStyle.Flat, DialogResult = DialogResult.Cancel };
        dlg.Controls.AddRange([lbl, btnMute, btnUnmute, btnCancel]);
        dlg.CancelButton = btnCancel;
        ThemeManager.Apply(dlg, ThemeManager.Current);

        var result = dlg.ShowDialog(this);
        if (result == DialogResult.Cancel) return;

        bool muting = result == DialogResult.Yes;
        bool ok = muting
            ? await _rconClient.MutePlayerAsync("127.0.0.1", _config.EchoPort, steamId)
            : await _rconClient.UnmutePlayerAsync("127.0.0.1", _config.EchoPort, steamId);

        string action = muting ? "Muted" : "Unmuted";
        rtbRconOutput.AppendConsoleLine(
            ok ? $"{action}: {name} ({steamId})" : $"{action} failed for: {name}",
            ok ? ThemeManager.StateRunning : ThemeManager.StateStopped);

        if (ok && muting)
            CachePlayerName(_configManager.MuteCachePath, steamId, name ?? steamId);
        else if (ok && !muting)
            RemoveFromCache(_configManager.MuteCachePath, steamId);
    }

    private async Task BroadcastAsync()
    {
        string msg = txtAnnouncement.Text.Trim();
        if (string.IsNullOrWhiteSpace(msg)) return;

        bool ok = await _rconClient.BroadcastAsync("127.0.0.1", _config.EchoPort, msg);
        rtbRconOutput.AppendConsoleLine(
            ok ? $"Broadcast sent: {msg}" : "Broadcast failed.",
            ok ? ThemeManager.StateRunning : ThemeManager.StateStopped);

        if (ok) txtAnnouncement.Clear();
    }

    private async Task TestWebhookAsync()
    {
        string url = txtWebhookUrl.Text.Trim();
        if (!DiscordWebhookService.IsValidUrl(url))
        {
            MessageBox.Show("Please enter a valid Discord webhook URL.", "Invalid URL",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        btnTestWebhook.Enabled = false;
        bool ok = await _discordService.TestAsync(url);
        MessageBox.Show(ok ? "Test notification sent successfully!" : "Failed to send test notification.\nCheck the webhook URL.",
            "Webhook Test", MessageBoxButtons.OK, ok ? MessageBoxIcon.Information : MessageBoxIcon.Error);
        btnTestWebhook.Enabled = true;
    }

    private void DgvPlayers_SelectionChanged(object? sender, EventArgs e)
    {
        bool hasSelection = dgvPlayers.SelectedRows.Count > 0;
        btnKickPlayer.Enabled    = hasSelection;
        btnBanPlayer.Enabled     = hasSelection;
        btnMutePlayer.Enabled    = hasSelection;
        btnMessagePlayer.Enabled = hasSelection;
    }

    private (string? Name, string? SteamId) GetSelectedPlayer()
    {
        if (dgvPlayers.SelectedRows.Count == 0) return (null, null);
        var row = dgvPlayers.SelectedRows[0];
        return (row.Cells["Name"].Value?.ToString(),
                row.Cells["SteamId"].Value?.ToString());
    }
}
