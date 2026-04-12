namespace SoulMaskServerManager.Forms;

partial class MainForm
{
    private async Task CreateBackupAsync()
    {
        btnCreateBackup.Enabled     = false;
        btnCreateBackupNow2.Enabled = false;
        AppendConsole("[Backup] Creating backup...", Color.FromArgb(156, 110, 201));

        string? path = await _backupService.CreateBackupAsync();
        if (path != null)
        {
            AppendConsole($"[Backup] Created: {Path.GetFileName(path)}", Color.FromArgb(78, 201, 176));
            RefreshBackupList();
        }
        else
        {
            AppendConsole("[Backup] Backup failed — see logs.", ThemeManager.StateStopped);
        }

        btnCreateBackup.Enabled     = true;
        btnCreateBackupNow2.Enabled = true;
    }

    private async Task RestoreBackupAsync()
    {
        if (dgvBackups.SelectedRows.Count == 0) return;
        string path = dgvBackups.SelectedRows[0].Tag?.ToString() ?? "";
        if (!File.Exists(path)) return;

        if (_serverManager.IsRunning)
        {
            MessageBox.Show("Stop the server before restoring a backup.",
                "Server Running", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var confirm = MessageBox.Show(
            $"Restore backup:\n{Path.GetFileName(path)}\n\nThis will overwrite your current save data. A safety backup will be created first.",
            "Confirm Restore", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
        if (confirm != DialogResult.Yes) return;

        btnRestoreBackup.Enabled = false;
        AppendConsole($"[Backup] Restoring {Path.GetFileName(path)}...", Color.FromArgb(156, 110, 201));

        bool ok = await _backupService.RestoreBackupAsync(path);
        AppendConsole(
            ok ? "[Backup] Restore complete." : "[Backup] Restore failed — see logs.",
            ok ? Color.FromArgb(78, 201, 176) : ThemeManager.StateStopped);

        btnRestoreBackup.Enabled = true;
        RefreshBackupList();
    }

    private void DeleteSelectedBackup()
    {
        if (dgvBackups.SelectedRows.Count == 0) return;
        string path = dgvBackups.SelectedRows[0].Tag?.ToString() ?? "";

        if (MessageBox.Show($"Delete backup:\n{Path.GetFileName(path)}?",
            "Confirm Delete", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;

        if (_backupService.DeleteBackup(path))
        {
            AppendConsole($"[Backup] Deleted: {Path.GetFileName(path)}", Color.Gray);
            RefreshBackupList();
        }
    }

    public void RefreshBackupList()
    {
        this.InvokeIfRequired(() =>
        {
            dgvBackups.Rows.Clear();
            foreach (var (path, date, size) in _backupService.GetBackups())
            {
                string sizeStr = size > 1_048_576
                    ? $"{size / 1_048_576.0:F1} MB"
                    : $"{size / 1024.0:F0} KB";

                int idx = dgvBackups.Rows.Add(
                    date.ToString("yyyy-MM-dd  HH:mm:ss"),
                    sizeStr,
                    Path.GetFileName(path));
                dgvBackups.Rows[idx].Tag = path;
            }
        });
    }

    private void DgvBackups_SelectionChanged(object? sender, EventArgs e)
    {
        bool sel = dgvBackups.SelectedRows.Count > 0;
        btnRestoreBackup.Enabled = sel;
        btnDeleteBackup.Enabled  = sel;
    }
}
