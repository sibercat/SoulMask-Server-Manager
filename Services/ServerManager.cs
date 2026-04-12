using System.Diagnostics;
using System.Runtime.InteropServices;

namespace SoulMaskServerManager.Services;

public class ServerManager
{
    private readonly string _serverFilesDir;
    private readonly FileLogger _logger;

    private Process? _serverProcess;
    private ServerState _state = ServerState.NotInstalled;
    private bool _isStopping;
    private int _restartAttempts;
    private ServerConfiguration? _lastConfig;

    // Crash detection config
    private bool _crashDetectionEnabled = true;
    private bool _autoRestart           = true;
    private int  _maxRestartAttempts    = 3;

    // Load detection
    private CancellationTokenSource? _loadWatcherCts;
    private string LogPath => Path.Combine(_serverFilesDir, "WS", "Saved", "Logs", "WS.log");
    private const string LoadedMarker = "logSoulmaskSession: [SERVER_LIST] registe server soulmask session succeed.";

    // ── Events ───────────────────────────────────────────────────────
    public event EventHandler<ServerState>? StateChanged;
    public event EventHandler<string>? OutputReceived;
    public event EventHandler? CrashDetected;
    public event EventHandler? AutoRestarted;

    // ── Properties ───────────────────────────────────────────────────
    public ServerState State => _state;
    public bool IsRunning    => _state == ServerState.Running;
    public int  ProcessId    => _serverProcess?.Id ?? 0;

    /// <summary>
    /// Called once at startup. Attaches to an already-running server process if found,
    /// otherwise sets Stopped/NotInstalled based on whether the exe exists.
    /// Matches CheckInitialState() pattern from reference project.
    /// </summary>
    public void InitializeState()
    {
        try
        {
            // Check if server is already running (e.g. launcher restarted)
            var shipping = FindShippingProcess();

            if (shipping != null)
            {
                _serverProcess = shipping;
                _serverProcess.EnableRaisingEvents = true;
                _serverProcess.Exited += OnExited;
                _logger.Info($"Attached to existing server process (PID {shipping.Id}).");
                SetState(ServerState.Running);
                return;
            }
        }
        catch { /* process enumeration failed — fall through */ }

        SetState(IsServerInstalled ? ServerState.Stopped : ServerState.NotInstalled);
    }

    // Run WSServer-Win64-Shipping.exe directly (not the wrapper WSServer.exe) so Steam DLLs
    // are resolved correctly on machines without a Steam client installed.
    // IsServerInstalled checks WSServer.exe as the install marker (placed by SteamCMD).
    private string ServerExe => Path.Combine(_serverFilesDir, "WS", "Binaries", "Win64", "WSServer-Win64-Shipping.exe");
    public bool IsServerInstalled => File.Exists(Path.Combine(_serverFilesDir, "WSServer.exe"));

    public ServerManager(string rootDir, FileLogger logger)
    {
        _serverFilesDir = Path.Combine(rootDir, "ServerFiles");
        _logger         = logger;
    }

    // ── Start ────────────────────────────────────────────────────────

    public void Start(ServerConfiguration cfg)
    {
        if (_state is ServerState.Running or ServerState.Starting) return;

        _lastConfig      = cfg;
        _isStopping      = false;
        _restartAttempts = 0;

        ConfigureCrashDetection(cfg.EnableCrashDetection, cfg.AutoRestart, cfg.MaxRestartAttempts);
        DoStart(cfg);
    }

    private void DoStart(ServerConfiguration cfg)
    {
        // Cancel any previous load watcher
        _loadWatcherCts?.Cancel();
        _loadWatcherCts = new CancellationTokenSource();

        SetState(ServerState.Starting);
        string args = BuildLaunchArgs(cfg);
        _logger.Info($"Starting server: {ServerExe} {args}");
        Emit($"Starting SoulMask server...");
        Emit($"Args: {args}");

        // Set SteamAppId env var so the shipping exe finds Steam — inherited by child process.
        Environment.SetEnvironmentVariable("SteamAppId", "2646460");

        // UseShellExecute=true: server gets its own console window and writes -log to WS/Saved/Logs/WS.log.
        var psi = new ProcessStartInfo
        {
            FileName         = ServerExe,
            Arguments        = args,
            UseShellExecute  = true,
            WorkingDirectory = Path.GetDirectoryName(ServerExe)
        };

        _serverProcess = new Process { StartInfo = psi, EnableRaisingEvents = true };
        _serverProcess.Exited += OnExited;

        _serverProcess.Start();
        _logger.Info($"Server process started (PID {_serverProcess.Id}).");

        ApplyPriority(cfg.ProcessPriority);
        ApplyCpuAffinity(cfg.UseAllCores, cfg.CpuAffinity);

        // Watch WS.log for the "session registered" line instead of setting Running immediately.
        // State stays Starting until the server has fully loaded.
        _ = WatchForServerLoadedAsync(_loadWatcherCts.Token);
    }

    // ── Load Detection ───────────────────────────────────────────────

    private async Task WatchForServerLoadedAsync(CancellationToken ct)
    {
        // Wait up to 60 s for WS.log to appear (the server creates it a few seconds after launch)
        for (int i = 0; i < 60 && !ct.IsCancellationRequested; i++)
        {
            if (File.Exists(LogPath)) break;
            await Task.Delay(1000, ct).ConfigureAwait(false);
        }

        if (ct.IsCancellationRequested) return;

        if (!File.Exists(LogPath))
        {
            _logger.Warning("WS.log not found after 60 s — marking server as Running anyway.");
            Emit("Could not detect load from WS.log — showing as Running.");
            if (_state == ServerState.Starting) SetState(ServerState.Running);
            return;
        }

        Emit("Waiting for server to finish loading (watching WS.log)...");

        try
        {
            using var fs = new FileStream(LogPath, FileMode.Open, FileAccess.Read,
                                          FileShare.ReadWrite | FileShare.Delete);
            using var sr = new StreamReader(fs);

            // Skip content from the previous run — seek to end so we only read
            // lines the server writes after this launch, not old log data.
            fs.Seek(0, SeekOrigin.End);

            // Skip content from previous run — seek to current end of file so we
            // only read lines the server writes after this launch.
            fs.Seek(0, SeekOrigin.End);

            var deadline = DateTime.UtcNow.AddMinutes(15);

            while (!ct.IsCancellationRequested && _state == ServerState.Starting)
            {
                string? line = await sr.ReadLineAsync(ct).ConfigureAwait(false);

                if (line == null)
                {
                    if (DateTime.UtcNow > deadline)
                    {
                        _logger.Warning("Server load timeout (15 min) — marking as Running.");
                        Emit("Server load timeout — showing as Running.");
                        SetState(ServerState.Running);
                        return;
                    }
                    await Task.Delay(500, ct).ConfigureAwait(false);
                    continue;
                }

                if (line.Contains(LoadedMarker, StringComparison.OrdinalIgnoreCase))
                {
                    _logger.Info("Server fully loaded (session registered in Steam).");
                    Emit("Server fully loaded and registered.");
                    SetState(ServerState.Running);
                    return;
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _logger.Warning($"Log watcher error: {ex.Message} — marking server as Running anyway.");
            if (!ct.IsCancellationRequested && _state == ServerState.Starting)
            {
                Emit("Log watcher error — showing as Running.");
                SetState(ServerState.Running);
            }
        }
    }

    // ── Stop ─────────────────────────────────────────────────────────

    public async Task StopAsync(RconClient? rcon = null, ServerConfiguration? cfg = null)
    {
        if (_state is ServerState.Stopped or ServerState.NotInstalled) return;

        _isStopping = true;
        _loadWatcherCts?.Cancel();
        SetState(ServerState.Stopping);
        Emit("Stopping server...");

        // 1. Try EchoPort save
        if (rcon != null && cfg != null)
        {
            Emit("Sending world save via EchoPort...");
            await rcon.SaveWorldAsync("127.0.0.1", cfg.EchoPort);
            await Task.Delay(3000);
        }

        // 2. Graceful Ctrl+C to the shipping process
        var shipping = FindShippingProcess();
        if (shipping != null)
        {
            Emit("Sending graceful shutdown signal...");
            SendCtrlC(shipping.Id);
            await Task.Run(() => shipping.WaitForExit(8000));
        }

        // 3. Force-kill the launcher process if still alive
        await KillProcessAsync(_serverProcess);

        // 4. Force-kill any remaining shipping process
        if (shipping != null && !shipping.HasExited)
            await KillProcessAsync(shipping);

        SetState(ServerState.Stopped);
        Emit("Server stopped.");
        _logger.Info("Server stopped by user.");
    }

    public async Task RestartAsync(RconClient? rcon, ServerConfiguration cfg)
    {
        await StopAsync(rcon, cfg);
        await Task.Delay(2000);
        DoStart(cfg);
    }

    // ── Crash Detection ──────────────────────────────────────────────

    public void ConfigureCrashDetection(bool enabled, bool autoRestart, int maxAttempts)
    {
        _crashDetectionEnabled = enabled;
        _autoRestart           = autoRestart;
        _maxRestartAttempts    = maxAttempts;
    }

    private void OnExited(object? sender, EventArgs e)
    {
        if (_isStopping) return; // Expected stop — don't treat as crash

        _loadWatcherCts?.Cancel();
        _logger.Warning("Server process exited unexpectedly.");
        Emit("Server process exited unexpectedly!");

        if (_crashDetectionEnabled)
        {
            CrashDetected?.Invoke(this, EventArgs.Empty);
            SetState(ServerState.Crashed);

            if (_autoRestart && _lastConfig != null && _restartAttempts < _maxRestartAttempts)
            {
                _restartAttempts++;
                Emit($"Auto-restarting... attempt {_restartAttempts}/{_maxRestartAttempts}");
                _logger.Info($"Auto-restart attempt {_restartAttempts}/{_maxRestartAttempts}");
                Thread.Sleep(5000);
                DoStart(_lastConfig);
                AutoRestarted?.Invoke(this, EventArgs.Empty);
            }
            else
            {
                Emit("Max restart attempts reached or auto-restart disabled.");
                SetState(ServerState.Stopped);
            }
        }
        else
        {
            SetState(ServerState.Stopped);
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────


    private Process? FindShippingProcess()
    {
        try
        {
            // Match only the process whose exe path is under this instance's ServerFiles dir.
            // Without this check, a second manager instance would attach to the first
            // manager's server process (GetProcessesByName returns ALL matching processes
            // on the machine regardless of which folder they were launched from).
            string expectedExe = Path.GetFullPath(ServerExe);
            foreach (string name in new[] { "WSServer-Win64-Shipping", "WSServer" })
            {
                var match = Process.GetProcessesByName(name).FirstOrDefault(p =>
                {
                    try { return string.Equals(Path.GetFullPath(p.MainModule!.FileName), expectedExe, StringComparison.OrdinalIgnoreCase); }
                    catch { return false; }
                });
                if (match != null) return match;
            }
        }
        catch { /* fall through */ }
        return null;
    }

    private static async Task KillProcessAsync(Process? proc)
    {
        if (proc == null || proc.HasExited) return;
        try
        {
            proc.Kill(entireProcessTree: true);
            await Task.Run(() => proc.WaitForExit(5000));
        }
        catch { /* already gone */ }
    }

    private static bool SendCtrlC(int pid)
    {
        try
        {
            NativeMethods.FreeConsole();
            if (!NativeMethods.AttachConsole(pid)) return false;
            NativeMethods.SetConsoleCtrlHandler(null, true);
            NativeMethods.GenerateConsoleCtrlEvent(NativeMethods.CTRL_C_EVENT, 0);
            Thread.Sleep(500);
            NativeMethods.FreeConsole();
            NativeMethods.SetConsoleCtrlHandler(null, false);
            return true;
        }
        catch { return false; }
    }

    private void ApplyPriority(string priority)
    {
        if (_serverProcess == null || _serverProcess.HasExited) return;
        try
        {
            _serverProcess.PriorityClass = priority switch
            {
                "AboveNormal" => ProcessPriorityClass.AboveNormal,
                "High"        => ProcessPriorityClass.High,
                _             => ProcessPriorityClass.Normal
            };
        }
        catch { /* Process may have ended */ }
    }

    private void ApplyCpuAffinity(bool useAll, string affinityMask)
    {
        if (_serverProcess == null || _serverProcess.HasExited) return;
        if (useAll || string.IsNullOrWhiteSpace(affinityMask)) return;
        try
        {
            if (long.TryParse(affinityMask, out long mask) && mask > 0)
                _serverProcess.ProcessorAffinity = (IntPtr)mask;
        }
        catch { /* Not critical */ }
    }

    private static string BuildLaunchArgs(ServerConfiguration cfg)
    {
        // Guide format: WSServer-Win64-Shipping.exe Level01_Main -server -forcepassthrough
        //               -UTF8Output -log -PORT=8777 -QueryPort=27015 -EchoPort=18888
        //               -SteamServerName="name" -MaxPlayers=20 -PSW="pw" -adminpsw="pw" -pve
        var sb = new StringBuilder();

        // Level name is positional arg #1 (required)
        sb.Append(string.IsNullOrWhiteSpace(cfg.MapName) ? "Level01_Main" : cfg.MapName);

        // Required flags
        sb.Append(" -server");
        sb.Append(" -forcepassthrough");
        sb.Append(" -UTF8Output");
        sb.Append(" -log");

        // Network
        sb.Append($" -PORT={cfg.GamePort}");
        sb.Append($" -QueryPort={cfg.QueryPort}");
        sb.Append($" -EchoPort={cfg.EchoPort}");

        // Identity
        sb.Append($" -SteamServerName=\"{cfg.ServerName}\"");
        sb.Append($" -MaxPlayers={cfg.MaxPlayers}");

        if (!string.IsNullOrWhiteSpace(cfg.ServerPassword))
            sb.Append($" -PSW=\"{cfg.ServerPassword}\"");

        if (!string.IsNullOrWhiteSpace(cfg.AdminPassword))
            sb.Append($" -adminpsw=\"{cfg.AdminPassword}\"");

        if (cfg.PveMode)
            sb.Append(" -pve");

        // RCON (optional — requires password + bind address)
        if (cfg.RconEnabled && !string.IsNullOrWhiteSpace(cfg.RconPassword))
        {
            sb.Append($" -rconpsw=\"{cfg.RconPassword}\"");
            sb.Append($" -rconaddr={cfg.RconAddress}");
            sb.Append($" -rconport={cfg.RconPort}");
        }

        // Server permissions bitmask — persists ban/mute/whitelist state across restarts.
        // Without this, all permission lists are disabled on every server restart.
        if (cfg.ServerPermissionMask > 0)
            sb.Append($" -serverpm={cfg.ServerPermissionMask}");

        // Cluster args
        if (cfg.ClusterRole != ClusterRole.Standalone)
        {
            sb.Append($" -serverid={cfg.ClusterId}");
            if (cfg.ClusterRole == ClusterRole.MainServer)
                sb.Append($" -mainserverport={cfg.ClusterMainPort}");
            else if (cfg.ClusterRole == ClusterRole.ClientServer && !string.IsNullOrWhiteSpace(cfg.ClusterClientConnect))
                sb.Append($" -clientserverconnect={cfg.ClusterClientConnect.Trim()}");
        }

        // Mods — must use backslash-escaped quotes so the server exe receives them literally
        if (cfg.Mods.Count > 0)
            sb.Append($" -mod=\\\"{string.Join(",", cfg.Mods)}\\\"");

        if (!string.IsNullOrWhiteSpace(cfg.CustomLaunchArgs))
            sb.Append($" {cfg.CustomLaunchArgs.Trim()}");

        return sb.ToString();
    }

    private void SetState(ServerState s)
    {
        _state = s;
        StateChanged?.Invoke(this, s);
    }


    private void Emit(string msg) => OutputReceived?.Invoke(this, msg);
}
