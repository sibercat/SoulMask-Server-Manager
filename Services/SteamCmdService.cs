using System.Diagnostics;
using System.IO.Compression;
using System.Net.Http;

namespace SoulMaskServerManager.Services;

public class SteamCmdService
{
    private const int    SOULMASK_APP_ID  = 3017310;   // Dedicated server App ID (game client = 2646460)
    private const string STEAM_APPID_TXT = "2646460";  // Written to steam_appid.txt next to server exe
    private const string STEAMCMD_ZIP_URL = "https://steamcdn-a.akamaihd.net/client/installer/steamcmd.zip";

    private readonly string _steamCmdDir;
    private readonly string _steamCmdExe;
    private readonly string _serverFilesDir;
    private readonly FileLogger _logger;

    private static readonly HttpClient _http = new();

    public event EventHandler<string>? OutputReceived;
    public event EventHandler<int>? DownloadProgressChanged; // 0-100

    public bool IsSteamCmdInstalled => File.Exists(_steamCmdExe);

    public bool IsServerInstalled => File.Exists(Path.Combine(_serverFilesDir, "WSServer.exe"));

    public SteamCmdService(string rootDir, FileLogger logger)
    {
        _steamCmdDir    = Path.Combine(rootDir, "steamcmd");
        _steamCmdExe    = Path.Combine(_steamCmdDir, "steamcmd.exe");
        _serverFilesDir = Path.Combine(rootDir, "ServerFiles");
        _logger         = logger;
    }

    // ── SteamCMD Download ────────────────────────────────────────────

    public async Task DownloadSteamCmdAsync(CancellationToken ct = default)
    {
        Directory.CreateDirectory(_steamCmdDir);
        string zipPath = Path.Combine(_steamCmdDir, "steamcmd.zip");

        Emit("Downloading SteamCMD...");
        _logger.Info("Downloading SteamCMD from Valve CDN.");

        using var response = await _http.GetAsync(STEAMCMD_ZIP_URL,
            HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        long? total = response.Content.Headers.ContentLength;
        await using (var src  = await response.Content.ReadAsStreamAsync(ct))
        await using (var dest = File.Create(zipPath))
        {
            byte[] buf = new byte[81920];
            long  read = 0;
            int   n;
            while ((n = await src.ReadAsync(buf, ct)) > 0)
            {
                await dest.WriteAsync(buf.AsMemory(0, n), ct);
                read += n;
                if (total > 0)
                    DownloadProgressChanged?.Invoke(this, (int)(read * 100 / total.Value));
            }
        } // dest and src are fully closed/flushed before extraction

        Emit("Extracting SteamCMD...");
        ZipFile.ExtractToDirectory(zipPath, _steamCmdDir, overwriteFiles: true);
        File.Delete(zipPath);

        Emit("Running SteamCMD first-time bootstrap (may take 30–60 seconds)...");
        await RunSteamCmdAsync("+quit", ct);

        _logger.Info("SteamCMD ready.");
        Emit("SteamCMD installed successfully.");
    }

    // ── Server Install / Update ──────────────────────────────────────

    public async Task InstallOrUpdateServerAsync(bool validate = false, CancellationToken ct = default)
    {
        if (!IsSteamCmdInstalled)
            await DownloadSteamCmdAsync(ct);

        Directory.CreateDirectory(_serverFilesDir);

        string args = $"+force_install_dir \"{_serverFilesDir}\" " +
                      $"+login anonymous " +
                      $"+app_update {SOULMASK_APP_ID}" +
                      (validate ? " validate" : "") +
                      " +quit";

        Emit(validate ? "Validating server files..." : "Installing/updating server files...");
        _logger.Info($"Running SteamCMD: {args}");

        bool success = false;
        void TrackSuccess(object? s, string line)
        {
            if (line.Contains($"App '{SOULMASK_APP_ID}'") && line.Contains("fully installed"))
                success = true;
        }
        OutputReceived += TrackSuccess;

        int exitCode = await RunSteamCmdAsync(args, ct);
        if (exitCode != 0 && !success)
        {
            // SteamCMD self-updates on first run — it needs a +quit pass to fully settle
            // before we can successfully install an app (otherwise we get "No subscription").
            Emit($"SteamCMD exited with code {exitCode}. Waiting for self-update to settle...");
            _logger.Info($"SteamCMD non-zero exit ({exitCode}) — running +quit settle pass, then retrying.");
            await RunSteamCmdAsync("+quit", ct);
            Emit("Retrying install...");
            await RunSteamCmdAsync(args, ct);
        }

        OutputReceived -= TrackSuccess;

        // steam_appid.txt is written by SteamCMD to WS\Binaries\Win64\ automatically.

        if (IsServerInstalled || success)
        {
            // Copy Steam DLLs from ServerFiles root to WS\Binaries\Win64\ so the
            // shipping exe can find them on machines without a Steam client installed.
            CopyServerDlls();
            Emit("Server installation complete.");
            _logger.Info("Server installation complete.");
        }
        else
        {
            Emit("SteamCMD finished. Check the log above for errors.");
            _logger.Warning("SteamCMD finished but server exe not found.");
        }
    }

    private void CopyServerDlls()
    {
        try
        {
            string binDir = Path.Combine(_serverFilesDir, "WS", "Binaries", "Win64");
            if (!Directory.Exists(binDir)) return;

            var dlls = Directory.GetFiles(_serverFilesDir, "*.dll");
            if (dlls.Length == 0) return;

            foreach (string dll in dlls)
                File.Copy(dll, Path.Combine(binDir, Path.GetFileName(dll)), overwrite: true);

            Emit($"Copied {dlls.Length} DLL(s) to WS\\Binaries\\Win64\\");
            _logger.Info($"Copied {dlls.Length} Steam DLL(s) to binaries directory.");
        }
        catch (Exception ex)
        {
            _logger.Warning($"DLL copy step failed (non-fatal): {ex.Message}");
            Emit($"Warning: DLL copy failed: {ex.Message}");
        }
    }

    // ── Mod Update ──────────────────────────────────────────────────

    /// <summary>Where the server loads mods from: ServerFiles\WS\Mods\{PluginName}\</summary>
    public string ModsDir => Path.Combine(_serverFilesDir, "WS", "Mods");

    /// <summary>Where SteamCMD downloads workshop items: steamcmd\steamapps\workshop\content\2646460\{modId}\</summary>
    public string WorkshopDownloadDir =>
        Path.Combine(_steamCmdDir, "steamapps", "workshop", "content", "2646460");

    public async Task UpdateModsAsync(List<string> modIds, CancellationToken ct = default)
    {
        if (!IsSteamCmdInstalled)
            await DownloadSteamCmdAsync(ct);

        if (modIds.Count == 0) { Emit("No mods to update."); return; }

        // Step 1 — download to SteamCMD's workshop folder
        var sb = new System.Text.StringBuilder();
        sb.Append("+login anonymous ");
        foreach (var id in modIds)
            sb.Append($"+workshop_download_item 2646460 {id} ");
        sb.Append("+quit");

        Emit($"Downloading {modIds.Count} mod(s) via SteamCMD...");
        _logger.Info($"Updating mods: {string.Join(", ", modIds)}");
        await RunSteamCmdAsync(sb.ToString(), ct);

        // Step 2 — copy each downloaded mod into ServerFiles\WS\Mods\{PluginName}\
        Emit("Copying mods to server mod folder...");
        Directory.CreateDirectory(ModsDir);

        foreach (var id in modIds)
        {
            ct.ThrowIfCancellationRequested();
            string downloadedPath = Path.Combine(WorkshopDownloadDir, id);
            if (!Directory.Exists(downloadedPath))
            {
                Emit($"  Warning: downloaded folder not found for mod {id}, skipping copy.");
                continue;
            }

            // ModeInfo.json may be at the root or nested in a subdirectory of the download
            string? modeInfoPath = Directory.GetFiles(downloadedPath, "ModeInfo.json", SearchOption.AllDirectories)
                                             .FirstOrDefault()
                                ?? Directory.GetFiles(downloadedPath, "ModInfo.json", SearchOption.AllDirectories)
                                             .FirstOrDefault();

            if (modeInfoPath == null)
            {
                Emit($"  Warning: ModeInfo.json not found for mod {id}, skipping copy.");
                continue;
            }

            // The folder containing ModeInfo.json is the mod root to copy
            string modRoot = Path.GetDirectoryName(modeInfoPath)!;

            string pluginName;
            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(modeInfoPath));
                pluginName = doc.RootElement.GetProperty("PluginName").GetString()
                             ?? throw new Exception("PluginName missing");
            }
            catch (Exception ex)
            {
                Emit($"  Warning: could not read PluginName from mod {id}: {ex.Message}");
                continue;
            }

            string destDir = Path.Combine(ModsDir, pluginName);
            Directory.CreateDirectory(destDir);

            foreach (var file in Directory.GetFiles(modRoot, "*", SearchOption.AllDirectories))
            {
                string rel  = Path.GetRelativePath(modRoot, file);
                string dest = Path.Combine(destDir, rel);
                Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                File.Copy(file, dest, overwrite: true);
            }

            // Stamp ModeInfo.json with the current time so Check for Updates
            // correctly shows "Up to date" after this install (File.Copy preserves
            // the depot's original timestamp, which predates any page-only edits).
            string stampPath = Path.Combine(destDir, "ModeInfo.json");
            if (File.Exists(stampPath))
                File.SetLastWriteTimeUtc(stampPath, DateTime.UtcNow);

            Emit($"  Mod {id} ({pluginName}) installed.");
        }

        Emit("Mod update complete.");
        _logger.Info("Mod update complete.");
    }

    // ── Copy from existing instance ──────────────────────────────────

    /// <summary>
    /// Copies all server files from <paramref name="sourceServerFilesDir"/> into this
    /// instance's ServerFiles folder, skipping WS\Saved\ (saves/logs are per-instance).
    /// Progress is reported as 0–100 via <see cref="DownloadProgressChanged"/>.
    /// </summary>
    public async Task CopyFromInstanceAsync(string sourceServerFilesDir, CancellationToken ct = default)
    {
        Directory.CreateDirectory(_serverFilesDir);

        Emit($"Scanning source files in {sourceServerFilesDir}...");
        var allFiles = Directory.GetFiles(sourceServerFilesDir, "*", SearchOption.AllDirectories);

        // Exclude WS\Saved\ — that folder holds saves, logs and per-instance configs
        string savedMarker = Path.Combine("WS", "Saved") + Path.DirectorySeparatorChar;
        var filesToCopy = allFiles
            .Where(f => !f.Contains(savedMarker, StringComparison.OrdinalIgnoreCase))
            .ToList();

        Emit($"Copying {filesToCopy.Count} files ({filesToCopy.Sum(f => new FileInfo(f).Length) / 1024 / 1024 / 1024.0:F1} GB)...");
        _logger.Info($"Copying {filesToCopy.Count} files from {sourceServerFilesDir}");

        int done = 0;
        foreach (var src in filesToCopy)
        {
            ct.ThrowIfCancellationRequested();

            string rel  = Path.GetRelativePath(sourceServerFilesDir, src);
            string dest = Path.Combine(_serverFilesDir, rel);

            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
            await Task.Run(() => File.Copy(src, dest, overwrite: true), ct);

            done++;
            int pct = (int)(done * 100L / filesToCopy.Count);
            DownloadProgressChanged?.Invoke(this, pct);

            if (done % 100 == 0)
                Emit($"  Copied {done} / {filesToCopy.Count} files...");
        }

        Emit("File copy complete.");
        _logger.Info("Instance copy complete.");
    }

    // ── Internal ─────────────────────────────────────────────────────

    private async Task<int> RunSteamCmdAsync(string arguments, CancellationToken ct = default)
    {
        var psi = new ProcessStartInfo
        {
            FileName               = _steamCmdExe,
            Arguments              = arguments,
            UseShellExecute        = false,
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            CreateNoWindow         = true,
            WorkingDirectory       = _steamCmdDir
        };

        using var proc = new Process { StartInfo = psi, EnableRaisingEvents = true };

        // Track last output time so we can emit a heartbeat during SteamCMD's silent self-update phase
        long lastOutputTick = Environment.TickCount64;

        proc.OutputDataReceived += (_, e) =>
        {
            if (e.Data == null) return;
            lastOutputTick = Environment.TickCount64;
            Emit(e.Data);
            ParseProgress(e.Data);
            if (e.Data.Contains("type 'quit' to exit"))
                Emit("SteamCMD is self-updating — this can take 1-2 minutes, please wait...");
        };
        proc.ErrorDataReceived += (_, e) => { if (e.Data != null) { lastOutputTick = Environment.TickCount64; Emit(e.Data); } };

        proc.Start();
        proc.BeginOutputReadLine();
        proc.BeginErrorReadLine();

        // Heartbeat: if SteamCMD goes silent for >30s, reassure the user it's still running
        using var heartbeatCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _ = Task.Run(async () =>
        {
            while (!heartbeatCts.Token.IsCancellationRequested)
            {
                await Task.Delay(30_000, heartbeatCts.Token).ContinueWith(_ => { });
                if (heartbeatCts.Token.IsCancellationRequested) break;
                long silentMs = Environment.TickCount64 - lastOutputTick;
                if (silentMs >= 28_000 && !proc.HasExited)
                    Emit($"  ... still working ({silentMs / 1000}s since last output) ...");
            }
        }, heartbeatCts.Token);

        await proc.WaitForExitAsync(ct);
        heartbeatCts.Cancel();
        return proc.ExitCode;
    }

    private void ParseProgress(string line)
    {
        // SteamCMD outputs lines like "Update state (0x5) downloading, progress: 12.34 (xxxxx / xxxxxx)"
        var match = System.Text.RegularExpressions.Regex.Match(line,
            @"progress:\s*([\d.]+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (match.Success && double.TryParse(match.Groups[1].Value,
            System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out double pct))
        {
            DownloadProgressChanged?.Invoke(this, (int)Math.Min(pct, 100));
        }
    }

    private void Emit(string msg) => OutputReceived?.Invoke(this, msg);
}
