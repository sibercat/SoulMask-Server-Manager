using System.IO.Compression;

namespace SoulMaskServerManager.Services;

public class BackupService
{
    private readonly string _serverFilesDir;
    private readonly string _backupsDir;
    private readonly FileLogger _logger;

    private System.Threading.Timer? _timer;
    private DateTime _nextBackupTime = DateTime.MaxValue;
    private bool _enabled;
    private int _intervalHours;
    private int _keepCount;

    public event EventHandler<string>? BackupCreated;
    public event EventHandler<string>? BackupFailed;

    public BackupService(string rootDir, FileLogger logger)
    {
        _serverFilesDir = Path.Combine(rootDir, "ServerFiles");
        _backupsDir     = Path.Combine(rootDir, "Backups");
        _logger         = logger;
        Directory.CreateDirectory(_backupsDir);
    }

    public void Configure(bool enabled, int intervalHours, int keepCount)
    {
        _enabled       = enabled;
        _intervalHours = Math.Max(1, intervalHours);
        _keepCount     = Math.Max(1, keepCount);

        _timer?.Dispose();
        if (!enabled) { _nextBackupTime = DateTime.MaxValue; return; }

        _nextBackupTime = DateTime.Now.AddHours(_intervalHours);
        _timer = new System.Threading.Timer(OnTimerTick, null,
            TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
    }

    private async void OnTimerTick(object? _)
    {
        if (_enabled && DateTime.Now >= _nextBackupTime)
        {
            _nextBackupTime = DateTime.Now.AddHours(_intervalHours);
            await CreateBackupAsync();
        }
    }

    public async Task<string?> CreateBackupAsync(string? preservePath = null)
    {
        string savedDir = Path.Combine(_serverFilesDir, "WS", "Saved");
        if (!Directory.Exists(savedDir))
        {
            string msg = "Saved folder not found — server has not been run yet.";
            _logger.Warning(msg);
            BackupFailed?.Invoke(this, msg);
            return null;
        }

        Directory.CreateDirectory(_backupsDir);
        string timestamp  = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        string backupPath = Path.Combine(_backupsDir, $"SoulMaskBackup_{timestamp}.zip");

        try
        {
            await Task.Run(() =>
            {
                using var archive = ZipFile.Open(backupPath, ZipArchiveMode.Create);
                foreach (string filePath in Directory.GetFiles(savedDir, "*", SearchOption.AllDirectories))
                {
                    // Skip the Logs folder
                    if (filePath.Contains(Path.Combine("Saved", "Logs") + Path.DirectorySeparatorChar) ||
                        filePath.Contains(Path.Combine("Saved", "Logs") + "/"))
                        continue;

                    try
                    {
                        string entryName = filePath[(savedDir.Length + 1)..];
                        archive.CreateEntryFromFile(filePath, entryName, CompressionLevel.Optimal);
                    }
                    catch (IOException) { /* File locked — skip gracefully */ }
                }
            });

            _logger.Info($"Backup created: {backupPath}");
            CleanOldBackups(preservePath);
            BackupCreated?.Invoke(this, backupPath);
            return backupPath;
        }
        catch (Exception ex)
        {
            _logger.Error("Backup failed.", ex);
            BackupFailed?.Invoke(this, ex.Message);
            if (File.Exists(backupPath)) File.Delete(backupPath);
            return null;
        }
    }

    public async Task<bool> RestoreBackupAsync(string backupPath)
    {
        string savedDir = Path.Combine(_serverFilesDir, "WS", "Saved");
        // Extract next to Saved so the final swap is a same-volume Directory.Move
        string tempDir = Path.Combine(_serverFilesDir, "WS", "_restore_tmp");
        try
        {
            // 1. Extract FIRST — a missing/corrupt zip fails here, before the
            //    live save is touched.
            await Task.Run(() =>
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, recursive: true);
                ZipFile.ExtractToDirectory(backupPath, tempDir);
            });

            // 2. Safety backup of the current state. Pass backupPath so the
            //    retention cleanup can't delete the zip being restored.
            await CreateBackupAsync(preservePath: backupPath);

            // 3. Swap the extracted files into place.
            await Task.Run(() =>
            {
                if (Directory.Exists(savedDir))
                    Directory.Delete(savedDir, recursive: true);
                Directory.Move(tempDir, savedDir);
            });

            _logger.Info($"Backup restored from: {backupPath}");
            return true;
        }
        catch (Exception ex)
        {
            _logger.Error("Restore failed.", ex);
            return false;
        }
        finally
        {
            try
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, recursive: true);
            }
            catch { /* best effort */ }
        }
    }

    public List<(string Path, DateTime Date, long SizeBytes)> GetBackups()
    {
        if (!Directory.Exists(_backupsDir)) return [];
        return Directory.GetFiles(_backupsDir, "*.zip")
            .Select(f => (f, File.GetLastWriteTime(f), new FileInfo(f).Length))
            .OrderByDescending(x => x.Item2)
            .ToList();
    }

    public bool DeleteBackup(string path)
    {
        try { File.Delete(path); return true; }
        catch (Exception ex) { _logger.Error("Failed to delete backup.", ex); return false; }
    }

    private void CleanOldBackups(string? preservePath = null)
    {
        var backups = GetBackups();
        foreach (var (path, _, _) in backups.Skip(_keepCount))
        {
            if (preservePath != null &&
                string.Equals(path, preservePath, StringComparison.OrdinalIgnoreCase))
                continue;
            try { File.Delete(path); _logger.Info($"Deleted old backup: {path}"); }
            catch { /* best effort */ }
        }
    }

    public DateTime NextBackupTime => _nextBackupTime;
    public string BackupsDirectory => _backupsDir;

    public void Dispose() => _timer?.Dispose();
}
