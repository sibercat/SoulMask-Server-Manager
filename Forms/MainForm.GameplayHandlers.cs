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

    private void RefreshPresetComboBox()
    {
        string? previousSelection = cmbGameplayPreset.SelectedItem?.ToString()
                                    ?? _config.LastGameplayPreset;
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
        else
        {
            cmbGameplayPreset.SelectedIndex = cmbGameplayPreset.Items.Count > 0 ? 0 : -1;
        }

        UpdatePresetButtons();
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
            lblGameplayStatus.Text = _gameplayPresets.Count == 0
                ? "Gameplay settings unavailable — install the server first."
                : "Preset not found.";
            return;
        }

        _currentPresetRows = preset.Select(kv => (kv.Key, kv.Value)).ToList();
        ApplyGameplayFilter(txtGameplaySearch.Text);
        // Custom presets always start dirty — reminds user to press Save to write to GameXishu.json.
        SetGameplayDirty(IsCustomPreset);
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
        if (string.IsNullOrWhiteSpace(name)) return;

        name = name.Trim();

        if (BuiltinKeys.Contains(name))
        {
            MessageBox.Show("Cannot use '0', '1', or '2' as a custom preset name.", "Invalid Name",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        bool isOverwrite = _customPresets.ContainsKey(name);
        if (isOverwrite && MessageBox.Show($"Overwrite existing preset '{name}'?", "Confirm",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;

        // Copy current values into custom presets
        _customPresets[name] = _currentPresetRows.ToDictionary(r => r.Key, r => r.Value);
        SaveCustomPresetToDisk(null); // save all custom presets

        // Clear dirty BEFORE RefreshPresetComboBox so SelectedIndexChanged
        // doesn't trigger the "unsaved changes" prompt during the switch
        SetGameplayDirty(false);
        RefreshPresetComboBox();

        // Select the new preset
        string target = $"{CustomPresetPrefix}{name}";
        int idx = cmbGameplayPreset.Items.IndexOf(target);
        if (idx >= 0) cmbGameplayPreset.SelectedIndex = idx;

        AppendConsole($"[Gameplay] Preset '{name}' saved.", Color.FromArgb(78, 201, 176));
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

        bool ok = await _rconClient.ExecuteAsync("127.0.0.1", _config.EchoPort, $"sc {key} {valStr}") != null;
        AppendConsole(
            ok ? $"[Gameplay] Applied live: {key} = {valStr}" : $"[Gameplay] Apply failed: {key}",
            ok ? Color.FromArgb(78, 201, 176) : ThemeManager.StateStopped);
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
