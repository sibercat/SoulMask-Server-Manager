namespace SoulMaskServerManager.Services;

/// <summary>
/// Handles persistence for launcher_settings.json, Game.ini and Engine.ini.
/// </summary>
public class ConfigurationManager
{
    private readonly string _rootDir;
    private readonly FileLogger _logger;

    private string SettingsFile   => Path.Combine(_rootDir, "launcher_settings.json");
    public  string ServerFilesDir => Path.Combine(_rootDir, "ServerFiles");
    private string SavedDir       => Path.Combine(ServerFilesDir, "WS", "Saved");
    private string ConfigDir      => Path.Combine(SavedDir, "Config", "WindowsServer");
    public  string GameIniPath    => Path.Combine(ConfigDir, "Game.ini");
    public  string EngineIniPath  => Path.Combine(ConfigDir, "Engine.ini");
    public  string GameplaySettingsPath   => Path.Combine(SavedDir, "GameplaySettings", "GameXishu.json");
    public  string GameplayDefaultsPath   => Path.Combine(SavedDir, "GameplaySettings", "GameXishu.default.json");
    public  string GameplayTemplatePath   => Path.Combine(ServerFilesDir, "WS", "Config", "GameplaySettings", "GameXishu_Template.json");
    public  string GameplayPresetsPath    => Path.Combine(_rootDir, "gameplay_presets.json");
    public  string BanListPath          => Path.Combine(SavedDir, "BlackAccountList.txt");
    public  string MuteListPath   => Path.Combine(SavedDir, "BanSpeek.txt");
    public  string BanCachePath   => Path.Combine(_rootDir, "banned_names.json");
    public  string MuteCachePath  => Path.Combine(_rootDir, "muted_names.json");

    private static readonly JsonSerializerOptions _jsonOpts = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public ConfigurationManager(string rootDir, FileLogger logger)
    {
        _rootDir = rootDir;
        _logger  = logger;
    }

    // ── Launcher Settings (JSON) ─────────────────────────────────────

    public ServerConfiguration LoadSettings()
    {
        if (!File.Exists(SettingsFile)) return new ServerConfiguration();
        try
        {
            string json = File.ReadAllText(SettingsFile);
            return JsonSerializer.Deserialize<ServerConfiguration>(json, _jsonOpts)
                   ?? new ServerConfiguration();
        }
        catch (Exception ex)
        {
            _logger.Error("Failed to load settings, using defaults.", ex);
            return new ServerConfiguration();
        }
    }

    public void SaveSettings(ServerConfiguration cfg)
    {
        try
        {
            Directory.CreateDirectory(_rootDir);
            File.WriteAllText(SettingsFile, JsonSerializer.Serialize(cfg, _jsonOpts));
        }
        catch (Exception ex)
        {
            _logger.Error("Failed to save settings.", ex);
        }
    }

    // ── Game.ini ─────────────────────────────────────────────────────

    public string ReadGameIni()
    {
        if (!File.Exists(GameIniPath)) return GenerateDefaultGameIni(new ServerConfiguration());
        try { return File.ReadAllText(GameIniPath); }
        catch (Exception ex) { _logger.Error("Failed to read Game.ini", ex); return ""; }
    }

    public void WriteGameIni(string content)
    {
        try
        {
            Directory.CreateDirectory(ConfigDir);
            File.WriteAllText(GameIniPath, content);
        }
        catch (Exception ex) { _logger.Error("Failed to write Game.ini", ex); }
    }

    public string GenerateDefaultGameIni(ServerConfiguration cfg) =>
$"""
[/Script/WS.WSGameMode]
ServerName={cfg.ServerName}
ServerPassword={cfg.ServerPassword}
AdminPassword={cfg.AdminPassword}
MaxPlayers={cfg.MaxPlayers}
SaveGameInterval={cfg.SaveInterval}
bPVEMode={cfg.PveMode.ToString().ToLower()}
DayTimeSpeedRate=1.0
NightTimeSpeedRate=1.0

[/Script/Engine.GameSession]
MaxPlayers={cfg.MaxPlayers}

""";

    // ── Engine.ini ───────────────────────────────────────────────────

    public string ReadEngineIni(ServerConfiguration? cfg = null)
    {
        if (!File.Exists(EngineIniPath)) return GenerateDefaultEngineIni(cfg ?? new ServerConfiguration());
        try { return File.ReadAllText(EngineIniPath); }
        catch (Exception ex) { _logger.Error("Failed to read Engine.ini", ex); return ""; }
    }

    public void WriteEngineIni(string content)
    {
        try
        {
            Directory.CreateDirectory(ConfigDir);
            File.WriteAllText(EngineIniPath, content);
        }
        catch (Exception ex) { _logger.Error("Failed to write Engine.ini", ex); }
    }

    public string GenerateDefaultEngineIni(ServerConfiguration cfg) =>
$"""
[URL]
Port={cfg.GamePort}

[OnlineSubsystemSteam]
GameServerQueryPort={cfg.QueryPort}

[Dedicated.Settings]
SteamServerName={cfg.ServerName}
MaxPlayers={cfg.MaxPlayers}
pvp={(cfg.PveMode ? "False" : "True")}
backup={cfg.GameBackupInterval}
saving={cfg.SaveInterval}

""";

    // ── INI Helpers ──────────────────────────────────────────────────

    public static string SetIniValue(string content, string section, string key, string value)
    {
        string sectionHeader = $"[{section}]";
        string keyValue      = $"{key}={value}";

        if (!content.Contains(sectionHeader))
            return content.TrimEnd() + $"\n\n{sectionHeader}\n{keyValue}\n";

        // Try replacing existing key in section
        var lines    = content.Split('\n').ToList();
        bool inSection = false;
        bool replaced  = false;

        for (int i = 0; i < lines.Count; i++)
        {
            string trimmed = lines[i].Trim();
            if (trimmed == sectionHeader) { inSection = true; continue; }
            if (trimmed.StartsWith('[') && inSection) { inSection = false; }
            if (inSection && trimmed.StartsWith(key + "=", StringComparison.OrdinalIgnoreCase))
            {
                lines[i] = keyValue;
                replaced  = true;
                break;
            }
        }

        if (!replaced)
        {
            // Insert after section header
            int idx = lines.FindIndex(l => l.Trim() == sectionHeader);
            lines.Insert(idx + 1, keyValue);
        }

        return string.Join("\n", lines);
    }
}
