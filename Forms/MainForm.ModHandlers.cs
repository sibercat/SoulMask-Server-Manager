#nullable enable
using System.Text.Json;

namespace SoulMaskServerManager.Forms;

partial class MainForm
{
    private static readonly HttpClient _httpMods = new()
    {
        Timeout = TimeSpan.FromSeconds(10)
    };

    // Cache fetched mod names and Steam timestamps so we don't re-hit the API
    private readonly Dictionary<string, string> _modNameCache      = new();
    private readonly Dictionary<string, long>   _modSteamTimestamp = new();
    private bool _modsDirty = false;

    // ── Load ──────────────────────────────────────────────────────────

    private void LoadModsTab()
    {
        lvMods.Items.Clear();

        int n = 1;
        foreach (var id in _config.Mods)
        {
            var item = new ListViewItem(n++.ToString());
            item.SubItems.Add(id);
            item.SubItems.Add(_modNameCache.TryGetValue(id, out string? cached) ? cached : "Loading...");
            item.SubItems.Add("");   // Status — filled by CheckModUpdatesAsync
            lvMods.Items.Add(item);
        }

        _modsDirty = false;
        UpdateModStatus();

        // Fetch names for any IDs not yet in cache
        var missing = _config.Mods.Where(id => !_modNameCache.ContainsKey(id)).ToList();
        if (missing.Count > 0)
            _ = FetchModNamesAsync(missing);
    }

    // ── Reorder ───────────────────────────────────────────────────────

    private void MoveSelectedMod(int direction)
    {
        if (lvMods.SelectedItems.Count == 0) return;
        var item  = lvMods.SelectedItems[0];
        int index = item.Index;
        int dest  = index + direction;
        if (dest < 0 || dest >= lvMods.Items.Count) return;

        lvMods.Items.RemoveAt(index);
        lvMods.Items.Insert(dest, item);
        item.Selected = true;
        lvMods.EnsureVisible(dest);
        RefreshOrderNumbers();
        _modsDirty = true;
        UpdateModStatus();
    }

    private void OnModDragDrop(object? sender, DragEventArgs e)
    {
        if (e.Data?.GetData(typeof(ListViewItem)) is not ListViewItem dragItem) return;
        var pt     = lvMods.PointToClient(new Point(e.X, e.Y));
        var target = lvMods.GetItemAt(pt.X, pt.Y);
        if (target == null || target == dragItem) return;

        int dest = target.Index;
        lvMods.Items.RemoveAt(dragItem.Index);
        lvMods.Items.Insert(dest, dragItem);
        dragItem.Selected = true;
        RefreshOrderNumbers();
        _modsDirty = true;
        UpdateModStatus();
    }

    private void RefreshOrderNumbers()
    {
        for (int i = 0; i < lvMods.Items.Count; i++)
            lvMods.Items[i].Text = (i + 1).ToString();
    }

    // ── Steam name lookup ──────────────────────────────────────────────

    private async Task FetchModNamesAsync(List<string> ids)
    {
        try
        {
            var formData = new List<KeyValuePair<string, string>>
            {
                new("itemcount", ids.Count.ToString())
            };
            for (int i = 0; i < ids.Count; i++)
                formData.Add(new($"publishedfileids[{i}]", ids[i]));

            var response = await _httpMods.PostAsync(
                "https://api.steampowered.com/ISteamRemoteStorage/GetPublishedFileDetails/v1/",
                new FormUrlEncodedContent(formData));

            if (!response.IsSuccessStatusCode) return;

            string json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);

            var details = doc.RootElement
                .GetProperty("response")
                .GetProperty("publishedfiledetails");

            foreach (var d in details.EnumerateArray())
            {
                if (!d.TryGetProperty("publishedfileid", out var idProp)) continue;
                string id = idProp.GetString() ?? "";
                if (string.IsNullOrEmpty(id)) continue;

                if (d.TryGetProperty("title", out var titleProp))
                    _modNameCache[id] = titleProp.GetString() ?? "Unknown";

                if (d.TryGetProperty("time_updated", out var timeProp))
                    _modSteamTimestamp[id] = timeProp.GetInt64();
            }

            // Update ListView items with fetched names + refresh status column
            this.InvokeIfRequired(() =>
            {
                foreach (ListViewItem item in lvMods.Items)
                {
                    string id = item.SubItems[1].Text;
                    if (_modNameCache.TryGetValue(id, out string? name))
                        item.SubItems[2].Text = name;
                }
            });
        }
        catch
        {
            this.InvokeIfRequired(() =>
            {
                foreach (ListViewItem item in lvMods.Items)
                    if (item.SubItems[2].Text == "Loading...")
                        item.SubItems[2].Text = "Could not fetch name";
            });
        }
    }

    // ── Add ───────────────────────────────────────────────────────────

    private void OnAddMod(object? sender, EventArgs e)
    {
        string input = txtModInput.Text.Trim();
        if (string.IsNullOrEmpty(input)) return;

        var newIds = input
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(id => id.All(char.IsDigit) && id.Length > 0)
            .ToList();

        if (newIds.Count == 0)
        {
            lblModStatus.Text = "Invalid ID — enter numeric Steam Workshop IDs only.";
            return;
        }

        var existing = lvMods.Items.Cast<ListViewItem>().Select(i => i.SubItems[1].Text).ToHashSet();
        var toAdd    = newIds.Where(id => !existing.Contains(id)).ToList();

        foreach (var id in toAdd)
        {
            var item = new ListViewItem((lvMods.Items.Count + 1).ToString());
            item.SubItems.Add(id);
            item.SubItems.Add(_modNameCache.TryGetValue(id, out string? cached) ? cached : "Loading...");
            item.SubItems.Add("");  // Status
            lvMods.Items.Add(item);
        }

        txtModInput.Clear();
        _modsDirty = true;
        UpdateModStatus();

        var needFetch = toAdd.Where(id => !_modNameCache.ContainsKey(id)).ToList();
        if (needFetch.Count > 0)
            _ = FetchModNamesAsync(needFetch);
    }

    // ── Remove ────────────────────────────────────────────────────────

    private void OnRemoveMod(object? sender, EventArgs e)
    {
        foreach (ListViewItem item in lvMods.SelectedItems.Cast<ListViewItem>().ToList())
            lvMods.Items.Remove(item);
        _modsDirty = true;
        UpdateModStatus();
    }

    // ── Open Workshop ─────────────────────────────────────────────────

    private void OnOpenWorkshop(object? sender, EventArgs e)
    {
        foreach (ListViewItem item in lvMods.SelectedItems)
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(
                $"https://steamcommunity.com/sharedfiles/filedetails/?id={item.SubItems[1].Text}")
                { UseShellExecute = true });
        }
    }

    // ── Save ──────────────────────────────────────────────────────────

    private void OnSaveMods(object? sender, EventArgs e)
    {
        _config.Mods = lvMods.Items.Cast<ListViewItem>().Select(i => i.SubItems[1].Text).ToList();
        _configManager.SaveSettings(_config);
        _modsDirty = false;
        int count = _config.Mods.Count;
        AppendConsole($"[Mods] Saved {count} mod{(count == 1 ? "" : "s")}.", Color.LightGreen);
        UpdateModStatus();
    }

    // ── Check for Updates ─────────────────────────────────────────────

    public async Task CheckModUpdatesAsync()
    {
        if (lvMods.Items.Count == 0) return;

        btnCheckModUpdates.Enabled = false;
        lblModStatus.Text = "Checking for updates...";

        var ids = lvMods.Items.Cast<ListViewItem>().Select(i => i.SubItems[1].Text).ToList();

        // Always re-fetch Steam timestamps so we catch updates approved since the app opened
        await FetchModNamesAsync(ids);

        // Scan ServerFiles\WS\Mods\ for installed mods by reading ModeInfo.json
        var installed = ScanInstalledMods(_steamCmd.ModsDir);

        this.InvokeIfRequired(() =>
        {
            int updates = 0;
            foreach (ListViewItem item in lvMods.Items)
            {
                string id = item.SubItems[1].Text;
                string status;

                if (!installed.TryGetValue(id, out var localInfo))
                {
                    status = "⬇ Not downloaded";
                    item.ForeColor = Color.FromArgb(97, 218, 251);
                }
                else if (_modSteamTimestamp.TryGetValue(id, out long steamTs))
                {
                    var steamDate = DateTimeOffset.FromUnixTimeSeconds(steamTs).UtcDateTime;
                    if (steamDate > localInfo.LastWrite.ToUniversalTime())
                    {
                        status = $"↑ Update available  v{localInfo.Version}";
                        updates++;
                        item.ForeColor = Color.FromArgb(255, 152, 0);
                    }
                    else
                    {
                        status = $"✓ Up to date  v{localInfo.Version}";
                        item.ForeColor = Color.FromArgb(134, 198, 100);
                    }
                }
                else
                {
                    status = $"✓ Installed  v{localInfo.Version}";
                    item.ForeColor = Color.FromArgb(134, 198, 100);
                }

                item.SubItems[3].Text = status;
            }

            lblModStatus.Text = updates > 0
                ? $"{updates} update{(updates == 1 ? "" : "s")} available"
                : "All mods up to date";
            btnCheckModUpdates.Enabled = true;
        });
    }

    private record ModLocalInfo(string Version, DateTime LastWrite);

    private static Dictionary<string, ModLocalInfo> ScanInstalledMods(string modsDir)
    {
        var result = new Dictionary<string, ModLocalInfo>();
        if (!Directory.Exists(modsDir)) return result;

        foreach (var dir in Directory.GetDirectories(modsDir))
        {
            // The game names folders with a hash (PluginName) — ModeInfo.json maps to ModID
            string jsonPath = Path.Combine(dir, "ModeInfo.json");
            if (!File.Exists(jsonPath))
                jsonPath = Path.Combine(dir, "ModInfo.json");
            if (!File.Exists(jsonPath)) continue;

            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(jsonPath));
                var root = doc.RootElement;
                string modId   = root.TryGetProperty("ModID",      out var idP)  ? idP.GetString()  ?? "" : "";
                string version = root.TryGetProperty("ModVersion",  out var verP) ? verP.GetString() ?? "" : "";
                if (string.IsNullOrEmpty(modId)) continue;
                result[modId] = new ModLocalInfo(version, File.GetLastWriteTimeUtc(jsonPath));
            }
            catch { /* skip unreadable folder */ }
        }

        return result;
    }

    // ── Update Mods ───────────────────────────────────────────────────

    public async Task OnUpdateModsAsync()
    {
        var ids = lvMods.Items.Cast<ListViewItem>().Select(i => i.SubItems[1].Text).ToList();
        if (ids.Count == 0) return;

        btnUpdateMods.Enabled      = false;
        btnCheckModUpdates.Enabled = false;

        var cts = new CancellationTokenSource();

        _steamCmd.OutputReceived      += OnSteamOutput;
        _steamCmd.DownloadProgressChanged += OnSteamProgress;

        try
        {
            await _steamCmd.UpdateModsAsync(ids, cts.Token);
            // Re-check status now that update is done
            await CheckModUpdatesAsync();
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            AppendConsole($"[Mods] Update error: {ex.Message}", Color.OrangeRed);
        }
        finally
        {
            _steamCmd.OutputReceived      -= OnSteamOutput;
            _steamCmd.DownloadProgressChanged -= OnSteamProgress;
            this.InvokeIfRequired(() =>
            {
                btnUpdateMods.Enabled      = true;
                btnCheckModUpdates.Enabled = true;
            });
        }
    }

    private void OnSteamOutput(object? sender, string line) =>
        this.InvokeIfRequired(() => AppendConsole($"[SteamCMD] {line}", Color.FromArgb(180, 180, 180)));

    private void OnSteamProgress(object? sender, int pct) =>
        this.InvokeIfRequired(() => { /* progress bar already handled by server install path */ });

    // ── Helpers ───────────────────────────────────────────────────────

    private void UpdateModStatus()
    {
        int count = lvMods.Items.Count;
        lblModStatus.Text = count == 0
            ? "No mods"
            : _modsDirty
                ? $"{count} mod{(count == 1 ? "" : "s")} — unsaved changes"
                : $"{count} mod{(count == 1 ? "" : "s")} active";
    }
}
