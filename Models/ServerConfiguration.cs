namespace SoulMaskServerManager.Models;

public class ServerConfiguration
{
    // ── Server Identity ──────────────────────────────────────────────
    public string ServerName { get; set; } = "SoulMask Server";
    public string ServerPassword { get; set; } = "";
    public string AdminPassword { get; set; } = "";

    // ── Network ──────────────────────────────────────────────────────
    public int GamePort { get; set; } = 8777;
    public int QueryPort { get; set; } = 27015;
    public int EchoPort { get; set; } = 18888;   // Telnet maintenance port (-EchoPort)
    // Permission bitmask: 1=account whitelist, 2=ban list, 4=IP whitelist, 8=IP blacklist, 16=mute list
    // Default 2 = ban list enabled. Without -serverpm, bans reset on every server restart.
    public int ServerPermissionMask { get; set; } = 2;

    // ── Gameplay ─────────────────────────────────────────────────────
    public string MapName { get; set; } = "Level01_Main"; // positional arg 1: Level01_Main or DLC_Level01_Main
    public int MaxPlayers { get; set; } = 20;
    public int SaveInterval { get; set; } = 600;  // seconds — maps to saving= in Engine.ini
    public int GameBackupInterval { get; set; } = 900; // seconds — maps to backup= in Engine.ini
    public bool PveMode { get; set; } = false;

    // ── Performance ──────────────────────────────────────────────────
    public string ProcessPriority { get; set; } = "Normal";
    public bool UseAllCores { get; set; } = true;
    public string CpuAffinity { get; set; } = "";

    // ── Crash Detection ──────────────────────────────────────────────
    public bool EnableCrashDetection { get; set; } = true;
    public bool AutoRestart { get; set; } = true;
    public int MaxRestartAttempts { get; set; } = 3;

    // ── Scheduled Restarts ───────────────────────────────────────────
    public bool ScheduledRestartEnabled { get; set; } = false;
    public bool UseFixedRestartTimes { get; set; } = false;
    public int RestartIntervalHours { get; set; } = 6;
    public string FixedRestartTimes { get; set; } = "03:00,15:00";
    public int RestartWarningMinutes { get; set; } = 10;
    public string RestartWarningMessage { get; set; } = "Server restarting in {minutes} minutes!";

    // ── Discord Webhooks ─────────────────────────────────────────────
    public bool EnableDiscordWebhook { get; set; } = false;
    public string DiscordWebhookUrl { get; set; } = "";
    public bool NotifyOnStart { get; set; } = true;
    public bool NotifyOnStop { get; set; } = true;
    public bool NotifyOnCrash { get; set; } = true;
    public bool NotifyOnRestart { get; set; } = true;
    public bool NotifyOnBackup { get; set; } = false;

    // ── Auto Backup ──────────────────────────────────────────────────
    public bool AutoBackupEnabled { get; set; } = false;
    public int BackupIntervalHours { get; set; } = 6;
    public int BackupKeepCount { get; set; } = 10;

    // ── RCON ─────────────────────────────────────────────────────────
    public bool RconEnabled { get; set; } = false;
    public string RconPassword { get; set; } = "";
    public int RconPort { get; set; } = 19000;
    public string RconAddress { get; set; } = "0.0.0.0";

    // ── Cluster ──────────────────────────────────────────────────────
    // Role:        Standalone = no cluster args added
    //              MainServer  = adds -serverid -mainserverport
    //              ClientServer= adds -serverid -clientserverconnect
    public ClusterRole ClusterRole { get; set; } = ClusterRole.Standalone;
    public int    ClusterId            { get; set; } = 1;
    public int    ClusterMainPort      { get; set; } = 20000;   // main server broadcast port
    public string ClusterClientConnect { get; set; } = "";      // "ip:port" of main server

    // ── Mods ─────────────────────────────────────────────────────────
    public List<string> Mods { get; set; } = [];

    // ── App / UI ─────────────────────────────────────────────────────
    public AppTheme Theme { get; set; } = AppTheme.Dark;
    public string CustomLaunchArgs { get; set; } = "";
    public string LastGameplayPreset { get; set; } = "0 — Server Preset";
}
