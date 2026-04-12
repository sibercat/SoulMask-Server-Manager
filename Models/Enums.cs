namespace SoulMaskServerManager.Models;

public enum ServerState
{
    NotInstalled,
    Installing,
    Stopped,
    Starting,
    Running,
    Stopping,
    Crashed
}

public enum LogLevel
{
    Debug,
    Info,
    Warning,
    Error
}

public enum AppTheme
{
    Dark,
    Light
}

public enum ClusterRole
{
    Standalone,
    MainServer,
    ClientServer
}
