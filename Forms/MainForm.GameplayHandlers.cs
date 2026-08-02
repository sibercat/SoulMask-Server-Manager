using System.Text.Json;
using System.Text.Json.Nodes;
using SoulMaskServerManager.Services;

namespace SoulMaskServerManager.Forms;

partial class MainForm
{
    // ── In-memory stores ──────────────────────────────────────────────
    // Server presets: keys "0", "1", "2" from GameXishu.json
    private Dictionary<string, Dictionary<string, double>> _gameplayPresets = [];
    // Custom named presets from gameplay_presets.json
    private Dictionary<string, Dictionary<string, double>> _customPresets   = [];

    private List<(string Key, double Value)> _currentPresetRows = [];
    private bool _gameplayDirty;

    // Settings edited since the last live apply — Apply Live pushes all of these,
    // not just whichever row happens to be selected.
    private readonly HashSet<string> _liveDirtyKeys = [];

    // Guards cmbGameplayPreset.SelectedIndexChanged while the combo is being
    // changed in code. Without it, restoring the selection after the user
    // declines to discard changes re-raises the event and re-prompts forever.
    private bool _suppressPresetChange;
    // The preset currently loaded into the grid, so a declined switch can be undone.
    private string? _activePresetItem;

    // Cell edits are written straight into the backing dictionary, so "lose
    // changes" has to restore from a snapshot taken when the preset was loaded —
    // otherwise the prompt only hides the badge and the edits still get saved.
    private Dictionary<string, double>? _presetSnapshot;
    private string? _snapshotKey;
    private bool    _snapshotIsCustom;

    // Scanning ~132 JSON files is slow enough to notice; templates don't change
    // while the app is open.
    private List<GameplayTemplate>? _templateCache;

    private const string CustomPresetPrefix = "★ ";
    private static readonly HashSet<string> BuiltinKeys = ["0", "1", "2"];

    private string SelectedPresetKey =>
        cmbGameplayPreset.SelectedItem?.ToString() is string s
            ? (s.StartsWith(CustomPresetPrefix) ? s[CustomPresetPrefix.Length..] : s.Split(' ')[0])
            : "0";

    private bool IsCustomPreset =>
        cmbGameplayPreset.SelectedItem?.ToString()?.StartsWith(CustomPresetPrefix) == true;

    // ── Load ──────────────────────────────────────────────────────────

    private void LoadGameplaySettings()
    {
        _gameplayPresets.Clear();
        _customPresets.Clear();

        // If GameXishu.json doesn't exist yet but the template does, seed it from the template
        // so users don't have to start the server once just to unlock the Gameplay tab.
        string serverPath = _configManager.GameplaySettingsPath;
        if (!File.Exists(serverPath) && File.Exists(_configManager.GameplayTemplatePath))
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(serverPath)!);
                File.Copy(_configManager.GameplayTemplatePath, serverPath);
                AppendConsole("[Gameplay] GameXishu.json seeded from server template.", Color.FromArgb(78, 201, 176));
            }
            catch { /* non-fatal — will just show "not found" message */ }
        }

        if (File.Exists(serverPath))
        {
            // Take a one-time backup on first load so we can always reset to original defaults
            string defaultsPath = _configManager.GameplayDefaultsPath;
            if (!File.Exists(defaultsPath))
            {
                try { File.Copy(serverPath, defaultsPath); } catch { }
            }

            try
            {
                var root = JsonNode.Parse(File.ReadAllText(serverPath))?.AsObject();
                if (root != null)
                {
                    foreach (var presetKv in root)
                    {
                        var dict = new Dictionary<string, double>();
                        if (presetKv.Value is JsonObject obj)
                            foreach (var kv in obj)
                                if (kv.Value is JsonValue jv && jv.TryGetValue<double>(out double d))
                                    dict[kv.Key] = d;
                        _gameplayPresets[presetKv.Key] = dict;
                    }
                }
            }
            catch (Exception ex)
            {
                lblGameplayStatus.Text = $"Load error: {ex.Message}";
            }
        }

        // Load custom presets from gameplay_presets.json
        string customPath = _configManager.GameplayPresetsPath;
        if (File.Exists(customPath))
        {
            try
            {
                _customPresets = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, double>>>(
                    File.ReadAllText(customPath)) ?? [];
            }
            catch { }
        }

        RefreshPresetComboBox();
        SetGameplayDirty(false);
    }

    private void RefreshPresetComboBox(bool populateGrid = true)
    {
        string? previousSelection = cmbGameplayPreset.SelectedItem?.ToString()
                                    ?? _config.LastGameplayPreset;

        // Rebuild without raising SelectedIndexChanged: Items.Clear() and the
        // SelectedIndex assignment below both fire it, which would run the
        // unsaved-changes prompt in the middle of a rebuild.
        _suppressPresetChange = true;
        try
        {
            cmbGameplayPreset.Items.Clear();

            // Built-in server presets
            foreach (var key in _gameplayPresets.Keys.OrderBy(k => k))
                cmbGameplayPreset.Items.Add($"{key} — Server Preset");

            // Custom presets (prefixed with star)
            foreach (var name in _customPresets.Keys.OrderBy(k => k))
                cmbGameplayPreset.Items.Add($"{CustomPresetPrefix}{name}");

            // Restore previous selection or default to first
            if (cmbGameplayPreset.Items.Count > 0)
            {
                int idx = previousSelection != null
                    ? cmbGameplayPreset.Items.IndexOf(previousSelection)
                    : -1;
                cmbGameplayPreset.SelectedIndex = idx >= 0 ? idx : 0;
            }
        }
        finally { _suppressPresetChange = false; }

        // The suppressed event would have refreshed these — do it explicitly.
        // Callers that immediately select another preset skip this to avoid
        // building the 276-row grid twice.
        if (populateGrid) PopulateGameplayGrid();
        UpdatePresetButtons();
    }

    /// <summary>
    /// Selects a preset in code and loads it, bypassing the unsaved-changes
    /// prompt — the caller has already decided this switch should happen.
    /// </summary>
    private void SelectPresetSilently(string item)
    {
        int idx = cmbGameplayPreset.Items.IndexOf(item);
        if (idx < 0) return;

        _suppressPresetChange = true;
        try { cmbGameplayPreset.SelectedIndex = idx; }
        finally { _suppressPresetChange = false; }

        PopulateGameplayGrid();
        UpdatePresetButtons();
        PersistSelectedPreset();   // the suppressed handler would have done this
    }

    /// <summary>Remembers the selected preset so it is restored on next launch.</summary>
    private void PersistSelectedPreset()
    {
        if (cmbGameplayPreset.SelectedItem?.ToString() is not string sel) return;
        _config.LastGameplayPreset = sel;
        _configManager.SaveSettings(_config);
    }

    /// <summary>
    /// Rolls the active preset back to the values it had when it was loaded.
    /// Cell edits mutate the backing dictionary directly, so without this the
    /// "lose changes" prompt would only hide the badge.
    /// </summary>
    private void DiscardPresetEdits()
    {
        if (_presetSnapshot == null || _snapshotKey == null) return;

        var target = _snapshotIsCustom
            ? (_customPresets.TryGetValue(_snapshotKey, out var c) ? c : null)
            : (_gameplayPresets.TryGetValue(_snapshotKey, out var s) ? s : null);
        if (target == null) return;

        target.Clear();
        foreach (var kv in _presetSnapshot) target[kv.Key] = kv.Value;
        _liveDirtyKeys.Clear();
    }

    /// <summary>
    /// Runs the unsaved-changes prompt. Returns false if the user wants to stay
    /// put; otherwise rolls the edits back and returns true.
    /// </summary>
    private bool ConfirmDiscardChanges()
    {
        if (!_gameplayDirty) return true;

        if (MessageBox.Show(
                "You have unsaved changes in the current preset.\nDiscard them?",
                "Unsaved Changes", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
            return false;

        DiscardPresetEdits();
        return true;
    }

    /// <summary>
    /// Puts the combo back to the preset actually loaded in the grid, without
    /// re-entering the change handler.
    /// </summary>
    private void RestorePresetSelection()
    {
        if (_activePresetItem == null) return;
        int idx = cmbGameplayPreset.Items.IndexOf(_activePresetItem);
        if (idx < 0 || idx == cmbGameplayPreset.SelectedIndex) return;

        _suppressPresetChange = true;
        try { cmbGameplayPreset.SelectedIndex = idx; }
        finally { _suppressPresetChange = false; }
    }

    private void PopulateGameplayGrid()
    {
        Dictionary<string, double>? preset = null;
        if (IsCustomPreset)
            _customPresets.TryGetValue(SelectedPresetKey, out preset);
        else
            _gameplayPresets.TryGetValue(SelectedPresetKey, out preset);

        if (preset == null)
        {
            dgvGameplay.Rows.Clear();
            // Clear tracking too — leaving it pointing at the previous preset made
            // a later Load Template merge against the wrong base.
            _currentPresetRows = [];
            _activePresetItem  = cmbGameplayPreset.SelectedItem?.ToString();
            _presetSnapshot    = null;
            _snapshotKey       = null;
            lblGameplayStatus.Text = _gameplayPresets.Count == 0
                ? "Gameplay settings unavailable — install the server first."
                : "Preset not found.";
            return;
        }

        _currentPresetRows = preset.Select(kv => (kv.Key, kv.Value)).ToList();
        _liveDirtyKeys.Clear();   // edits belong to the preset they were made in
        _activePresetItem  = cmbGameplayPreset.SelectedItem?.ToString();

        // Snapshot the pristine values so edits can genuinely be rolled back
        _presetSnapshot    = new Dictionary<string, double>(preset);
        _snapshotKey       = SelectedPresetKey;
        _snapshotIsCustom  = IsCustomPreset;

        ApplyGameplayFilter(txtGameplaySearch.Text);

        // Merely selecting a preset is not an edit. Custom presets used to be
        // flagged dirty here as a "remember to Save" nudge, but that claimed
        // unsaved changes for a preset already saved to disk and made every
        // switch away from it pop the discard-changes prompt.
        SetGameplayDirty(false);
    }

    private void ApplyGameplayFilter(string filter)
    {
        dgvGameplay.SuspendLayout();
        dgvGameplay.Rows.Clear();

        bool hasFilter = !string.IsNullOrWhiteSpace(filter);
        var rows = hasFilter
            ? _currentPresetRows.Where(r => r.Key.Contains(filter, StringComparison.OrdinalIgnoreCase)).ToList()
            : _currentPresetRows;

        foreach (var (key, value) in rows)
            dgvGameplay.Rows.Add(key, FormatValue(value));

        lblGameplayStatus.Text = hasFilter
            ? $"Showing {rows.Count} of {_currentPresetRows.Count} settings"
            : $"{_currentPresetRows.Count} settings";

        dgvGameplay.ResumeLayout();
    }

    // ── Save server preset ────────────────────────────────────────────

    private void SaveGameplaySettings()
    {
        if (IsCustomPreset)
        {
            SaveCustomPresetToDisk(SelectedPresetKey);
            WriteCustomPresetToGameXishu(SelectedPresetKey);
            SetGameplayDirty(false);
            AppendConsole($"[Gameplay] Custom preset '{SelectedPresetKey}' saved and written to GameXishu.json. Restart server to apply.", Color.FromArgb(78, 201, 176));
            return;
        }

        string path = _configManager.GameplaySettingsPath;
        try
        {
            var root = new JsonObject();
            foreach (var preset in _gameplayPresets)
            {
                var obj = new JsonObject();
                foreach (var kv in preset.Value)
                    obj[kv.Key] = JsonValue.Create(kv.Value);
                root[preset.Key] = obj;
            }
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
            SetGameplayDirty(false);
            AppendConsole("[Gameplay] Saved to GameXishu.json. Restart server to apply.", Color.FromArgb(78, 201, 176));
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to save: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    // ── Save As new custom preset ─────────────────────────────────────

    private void SaveAsNewPreset()
    {
        // Get current values from grid (reflects any edits)
        if (_currentPresetRows.Count == 0)
        {
            MessageBox.Show("No settings loaded to save.", "Nothing to Save",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        string? name = ShowInputDialog("Enter a name for this preset:", "Save As New Preset",
            IsCustomPreset ? SelectedPresetKey : "");

        var values = _currentPresetRows.ToDictionary(r => r.Key, r => r.Value);
        if (!CommitCustomPreset(name, values)) return;

        AppendConsole($"[Gameplay] Preset '{name!.Trim()}' saved.", Color.FromArgb(78, 201, 176));
    }

    // ── Load a preset shipped with the game ───────────────────────────

    private void LoadGameplayTemplate()
    {
        // Loading replaces what's in the grid — same contract as switching presets
        if (!ConfirmDiscardChanges()) return;

        List<GameplayTemplate> templates;
        var previousCursor = Cursor.Current;
        Cursor.Current = Cursors.WaitCursor;
        try
        {
            templates = _templateCache ??= GameplayTemplateService.Scan(_configManager.GameplayTemplatesDir);
        }
        finally { Cursor.Current = previousCursor; }

        if (templates.Count == 0)
        {
            _templateCache = null;   // don't cache "nothing installed"
            MessageBox.Show(
                "No preset files found.\n\nThey ship with the server files at:\n" +
                $"{_configManager.GameplayTemplatesDir}\n\nInstall the server first.",
                "No Templates Found", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        // "Partial" must be judged against a full preset, NOT against whatever is
        // currently selected — otherwise loading a 42-key overlay while a 42-key
        // preset is selected looks "full" and silently drops the other 234 settings.
        int fullCount = templates.Max(t => t.DefinedCount);

        var chosen = ShowTemplatePicker(templates, fullCount);
        if (chosen == null) return;

        bool isPartial = chosen.DefinedCount < fullCount;

        var merged = new Dictionary<string, double>();
        if (isPartial)
            foreach (var (key, value) in _currentPresetRows)   // base = what's loaded now
                merged[key] = value;
        foreach (var kv in chosen.Values)                       // template wins
            merged[kv.Key] = kv.Value;

        string? name = ShowInputDialog(
            isPartial
                ? $"'{chosen.DisplayName}' defines {chosen.DefinedCount} of {fullCount} settings.\n" +
                  "The rest keep their current values.\n\nSave as preset named:"
                : $"'{chosen.DisplayName}' defines {chosen.DefinedCount} settings.\n\nSave as preset named:",
            "Load Template", chosen.DisplayName);

        if (!CommitCustomPreset(name, merged)) return;

        AppendConsole(
            $"[Gameplay] Loaded '{chosen.FileName}' (preset {chosen.SlotKey}) — {chosen.DefinedCount} setting(s) " +
            $"{(isPartial ? "merged into" : "into")} preset '{name!.Trim()}'. Press Save to apply.",
            Color.FromArgb(78, 201, 176));
    }

    /// <summary>
    /// Validates a preset name, stores the values, persists them and selects the
    /// preset. Shared by Save As… and Load Template…. Returns false if cancelled.
    /// </summary>
    private bool CommitCustomPreset(string? name, Dictionary<string, double> values)
    {
        if (string.IsNullOrWhiteSpace(name)) return false;
        name = name.Trim();

        if (BuiltinKeys.Contains(name))
        {
            MessageBox.Show("Cannot use '0', '1', or '2' as a custom preset name.", "Invalid Name",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }

        if (_customPresets.ContainsKey(name) &&
            MessageBox.Show($"Overwrite existing preset '{name}'?", "Confirm",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return false;

        _customPresets[name] = values;
        SaveCustomPresetToDisk(null);

        SetGameplayDirty(false);
        RefreshPresetComboBox(populateGrid: false);   // the select below loads the grid
        SelectPresetSilently($"{CustomPresetPrefix}{name}");
        return true;
    }

    private GameplayTemplate? ShowTemplatePicker(List<GameplayTemplate> templates, int currentCount)
    {
        using var dlg = new Form
        {
            Text            = "Load Gameplay Template",
            Size            = new Size(620, 520),
            StartPosition   = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.Sizable,
            MinimizeBox     = false, MaximizeBox = false,
            MinimumSize     = new Size(520, 400)
        };

        var lv = new ListView
        {
            Dock          = DockStyle.Fill,
            View          = View.Details,
            FullRowSelect = true,
            MultiSelect   = false,
            HideSelection = false
        };
        lv.Columns.Add("Preset",   300);
        lv.Columns.Add("Settings",  80, HorizontalAlignment.Right);
        lv.Columns.Add("Type",     170);

        void Populate(string filter)
        {
            lv.BeginUpdate();
            lv.Items.Clear();
            foreach (var t in templates)
            {
                if (filter.Length > 0 &&
                    t.DisplayName.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0) continue;

                bool partial = currentCount > 0 && t.DefinedCount < currentCount;
                var item = new ListViewItem(t.DisplayName) { Tag = t };
                item.SubItems.Add(t.DefinedCount.ToString());
                item.SubItems.Add(partial ? "Partial — merges" : "Full preset");
                if (partial) item.ForeColor = Color.FromArgb(255, 152, 0);
                lv.Items.Add(item);
            }
            lv.EndUpdate();
        }

        var pnlTop = new Panel { Dock = DockStyle.Top, Height = 62, Padding = new Padding(10, 8, 10, 4) };
        var lblHint = new Label
        {
            Text     = "Presets shipped with the game. \"Partial\" ones only override some settings —\n" +
                       "the rest keep their current values. Nothing is saved until you press Save.",
            AutoSize = true, Location = new Point(10, 4), Tag = "sub"
        };
        var txtFilter = new TextBox
        {
            Location = new Point(10, 36), Width = 260, PlaceholderText = "Filter presets..."
        };
        txtFilter.TextChanged += (_, _) => Populate(txtFilter.Text.Trim());
        pnlTop.Controls.AddRange([lblHint, txtFilter]);

        var pnlBottom = new Panel { Dock = DockStyle.Bottom, Height = 46, Padding = new Padding(10, 8, 10, 8) };
        var btnOk     = new Button { Text = "Load",   Size = new Size(90, 30), DialogResult = DialogResult.OK,     FlatStyle = FlatStyle.Flat, Tag = "accent", Enabled = false };
        var btnCancel = new Button { Text = "Cancel", Size = new Size(90, 30), DialogResult = DialogResult.Cancel, FlatStyle = FlatStyle.Flat };
        btnOk.Location     = new Point(pnlBottom.Width - 200, 8);
        btnCancel.Location = new Point(pnlBottom.Width - 104, 8);
        btnOk.Anchor     = AnchorStyles.Top | AnchorStyles.Right;
        btnCancel.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        pnlBottom.Controls.AddRange([btnOk, btnCancel]);

        lv.SelectedIndexChanged += (_, _) => btnOk.Enabled = lv.SelectedItems.Count > 0;
        lv.DoubleClick          += (_, _) => { if (lv.SelectedItems.Count > 0) dlg.DialogResult = DialogResult.OK; };

        // Fill first, Top/Bottom after — WinForms docks last-added first
        dlg.Controls.Add(lv);
        dlg.Controls.Add(pnlTop);
        dlg.Controls.Add(pnlBottom);

        dlg.AcceptButton = btnOk;
        dlg.CancelButton = btnCancel;

        Populate("");
        ThemeManager.Apply(dlg, ThemeManager.Current);

        return dlg.ShowDialog(this) == DialogResult.OK && lv.SelectedItems.Count > 0
            ? lv.SelectedItems[0].Tag as GameplayTemplate
            : null;
    }

    // ── Delete custom preset ──────────────────────────────────────────

    private void DeleteCurrentCustomPreset()
    {
        if (!IsCustomPreset) return;
        string name = SelectedPresetKey;

        if (MessageBox.Show($"Delete preset '{name}'?", "Confirm Delete",
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;

        _customPresets.Remove(name);
        SaveCustomPresetToDisk(null);
        RefreshPresetComboBox();
        AppendConsole($"[Gameplay] Preset '{name}' deleted.", Color.Gray);
    }

    private void SaveCustomPresetToDisk(string? specificName)
    {
        try
        {
            if (specificName != null && _customPresets.ContainsKey(specificName))
                _customPresets[specificName] = _currentPresetRows.ToDictionary(r => r.Key, r => r.Value);

            File.WriteAllText(_configManager.GameplayPresetsPath,
                JsonSerializer.Serialize(_customPresets, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to save preset: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    // Writes all keys from a custom preset into GameXishu.json (all server presets),
    // so the server picks up the changes on next restart.
    private void WriteCustomPresetToGameXishu(string presetName)
    {
        if (!_customPresets.TryGetValue(presetName, out var customValues)) return;

        string path = _configManager.GameplaySettingsPath;
        try
        {
            // Merge custom preset values into every server preset (0, 1, 2)
            foreach (var key in _gameplayPresets.Keys)
                foreach (var kv in customValues)
                    _gameplayPresets[key][kv.Key] = kv.Value;

            var root = new JsonObject();
            foreach (var preset in _gameplayPresets)
            {
                var obj = new JsonObject();
                foreach (var kv in preset.Value)
                    obj[kv.Key] = JsonValue.Create(kv.Value);
                root[preset.Key] = obj;
            }
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to write to GameXishu.json: {ex.Message}", "Error",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void UpdatePresetButtons()
    {
        btnDeletePreset.Enabled = IsCustomPreset;
    }

    // ── Reset to Defaults ─────────────────────────────────────────────

    private void ResetGameplayToDefaults()
    {
        string defaultsPath = _configManager.GameplayDefaultsPath;
        if (!File.Exists(defaultsPath))
        {
            MessageBox.Show(
                "No defaults backup found.\nThe backup is created automatically the first time GameXishu.json is loaded.\nTry reloading the Gameplay tab.",
                "No Defaults Available", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        string target = IsCustomPreset ? $"custom preset '{SelectedPresetKey}'" : $"server preset {SelectedPresetKey}";
        if (MessageBox.Show(
                $"Reset {target} to the original game defaults?\nAll current values will be replaced.",
                "Confirm Reset", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;

        try
        {
            var root = JsonNode.Parse(File.ReadAllText(defaultsPath))?.AsObject();
            if (root == null) throw new InvalidDataException("Invalid defaults file.");

            // Use preset "0" from the defaults file as the canonical defaults
            var defaultPreset = new Dictionary<string, double>();
            if (root["0"] is JsonObject obj)
                foreach (var kv in obj)
                    if (kv.Value is JsonValue jv && jv.TryGetValue<double>(out double d))
                        defaultPreset[kv.Key] = d;

            if (IsCustomPreset)
                _customPresets[SelectedPresetKey] = defaultPreset;
            else
                _gameplayPresets[SelectedPresetKey] = defaultPreset;

            _currentPresetRows = defaultPreset.Select(kv => (kv.Key, kv.Value)).ToList();
            ApplyGameplayFilter(txtGameplaySearch.Text);
            SetGameplayDirty(true);
            AppendConsole($"[Gameplay] {target} reset to defaults. Save to apply.", Color.FromArgb(255, 152, 0));
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to load defaults: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    // ── Apply Live ────────────────────────────────────────────────────

    private async Task ApplyGameplaySettingLiveAsync()
    {
        if (!_serverManager.IsRunning)
        {
            MessageBox.Show("Apply Live sends settings to a running server.\nStart the server first.",
                "Server Not Running", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        // Everything edited since the last apply — falling back to the selected
        // row so a single setting can still be (re)applied on demand.
        var pending = _currentPresetRows
            .Where(r => _liveDirtyKeys.Contains(r.Key))
            .Select(r => (r.Key, Value: FormatValue(r.Value)))
            .ToList();

        if (pending.Count == 0)
        {
            if (dgvGameplay.CurrentRow == null) return;

            string key    = dgvGameplay.CurrentRow.Cells["Setting"].Value?.ToString() ?? "";
            string valStr = dgvGameplay.CurrentRow.Cells["Value"].Value?.ToString() ?? "";
            if (string.IsNullOrWhiteSpace(key)) return;

            if (!double.TryParse(valStr, System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out _))
            {
                MessageBox.Show($"'{valStr}' is not a valid number.", "Invalid Value",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            pending.Add((key, valStr));
        }

        btnApplyLive.Enabled = false;
        try
        {
            AppendConsole($"[Gameplay] Applying {pending.Count} setting{(pending.Count == 1 ? "" : "s")} live...",
                Color.FromArgb(255, 152, 0));

            // One connection for the whole batch — reconnecting per setting is
            // slow enough to look like a hang once more than a few are pending.
            var responses = await _rconClient.ExecuteManyAsync(
                "127.0.0.1", _config.EchoPort,
                pending.Select(p => $"sc {p.Key} {p.Value}"));

            if (responses == null)
            {
                AppendConsole("[Gameplay] Apply failed — could not reach the server on EchoPort.",
                    ThemeManager.StateStopped);
                return;
            }

            int applied = 0;
            for (int i = 0; i < pending.Count; i++)
            {
                bool ok = i < responses.Count && responses[i] != null;
                if (ok)
                {
                    applied++;
                    _liveDirtyKeys.Remove(pending[i].Key);
                }
                AppendConsole(
                    ok ? $"[Gameplay]   {pending[i].Key} = {pending[i].Value}"
                       : $"[Gameplay]   {pending[i].Key} — FAILED",
                    ok ? Color.FromArgb(78, 201, 176) : ThemeManager.StateStopped);
            }

            AppendConsole(
                $"[Gameplay] Applied {applied}/{pending.Count} live. " +
                "Live changes do NOT persist across a restart — press Save to keep them.",
                applied == pending.Count ? Color.FromArgb(78, 201, 176) : ThemeManager.StateStopped);
        }
        finally
        {
            btnApplyLive.Enabled = true;
        }
    }

    // ── Grid cell edit ────────────────────────────────────────────────

    private void DgvGameplay_CellValueChanged(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0 || dgvGameplay.Columns["Value"] is not { } valCol
            || e.ColumnIndex != valCol.Index) return;

        string key    = dgvGameplay.Rows[e.RowIndex].Cells["Setting"].Value?.ToString() ?? "";
        string valStr = dgvGameplay.Rows[e.RowIndex].Cells["Value"].Value?.ToString() ?? "";

        if (string.IsNullOrWhiteSpace(key)) return;

        if (!double.TryParse(valStr, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out double parsed)) return;

        // Update in-memory store
        Dictionary<string, double>? preset = IsCustomPreset
            ? (_customPresets.TryGetValue(SelectedPresetKey, out var cp) ? cp : null)
            : (_gameplayPresets.TryGetValue(SelectedPresetKey, out var sp) ? sp : null);

        if (preset != null)
            preset[key] = parsed;

        for (int i = 0; i < _currentPresetRows.Count; i++)
        {
            if (_currentPresetRows[i].Key == key)
            {
                _currentPresetRows[i] = (key, parsed);
                break;
            }
        }

        _liveDirtyKeys.Add(key);
        SetGameplayDirty(true);
    }

    private void DgvGameplay_CellBeginEdit(object? sender, DataGridViewCellCancelEventArgs e)
    {
        if (dgvGameplay.Columns["Value"] is not { } valCol || e.ColumnIndex != valCol.Index)
            e.Cancel = true;
    }

    private void DgvGameplay_SelectionChanged(object? sender, EventArgs e)
    {
        if (dgvGameplay.CurrentRow == null)
        {
            lblGameplayDescription.Text = "Select a setting to see its description.";
            return;
        }

        string key = dgvGameplay.CurrentRow.Cells["Setting"].Value?.ToString() ?? "";
        if (string.IsNullOrEmpty(key))
        {
            lblGameplayDescription.Text = "";
            return;
        }

        string desc     = GameplaySettingDescriptions.Get(key);
        string category = GameplaySettingDescriptions.GetCategory(key);

        lblGameplayDescription.Text = string.IsNullOrEmpty(desc)
            ? key
            : string.IsNullOrEmpty(category)
                ? desc
                : $"[{category}]  {desc}";
    }

    // ── Helpers ───────────────────────────────────────────────────────

    private void SetGameplayDirty(bool dirty)
    {
        _gameplayDirty = dirty;
        lblGameplayDirty.Visible = dirty;
    }

    private static string FormatValue(double value)
    {
        if (value == Math.Floor(value) && !double.IsInfinity(value) && Math.Abs(value) < 1e15)
            return ((long)value).ToString();
        return value.ToString("G6", System.Globalization.CultureInfo.InvariantCulture);
    }
}
