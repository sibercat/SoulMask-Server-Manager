using System.Net.Sockets;
using System.Text.RegularExpressions;

namespace SoulMaskServerManager.Services;

/// <summary>
/// SoulMask EchoPort maintenance client (Telnet-style TCP).
/// Connect → send command as UTF-8 line → read response → disconnect.
/// Specified via -EchoPort on the server command line (default 18888).
/// Wiki: https://soulmask.fandom.com/wiki/Dedicated_Server_Setup
/// </summary>
public class RconClient
{
    private const int CONNECT_TIMEOUT_MS = 5_000;
    private const int READ_TIMEOUT_MS    = 3_000;
    // The server does not close the connection after replying, so a response is
    // considered finished once no new bytes arrive for this long.
    private const int IDLE_TIMEOUT_MS    = 300;

    private readonly FileLogger _logger;
    public RconClient(FileLogger logger) => _logger = logger;

    // ── Public API ───────────────────────────────────────────────────

    public async Task<bool> TestConnectionAsync(string host, int port)
    {
        try
        {
            using var client = await ConnectAsync(host, port);
            return client.Connected;
        }
        catch (Exception ex)
        {
            _logger.Warning($"EchoPort test connection failed: {ex.Message}");
            return false;
        }
    }

    public async Task<string?> ExecuteAsync(string host, int port, string command)
    {
        try
        {
            using var client = await ConnectAsync(host, port);
            var stream = client.GetStream();
            stream.WriteTimeout = READ_TIMEOUT_MS;

            await SendAsync(stream, command);
            return await ReadResponseAsync(client, stream);
        }
        catch (Exception ex)
        {
            _logger.Warning($"EchoPort command '{command}' failed: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Runs several commands over a SINGLE connection and returns each response
    /// (null entry = that command produced no reply). Reusing one connection
    /// matters for bulk work: reconnecting per command costs a full
    /// connect/handshake/idle-wait cycle each time.
    /// </summary>
    public async Task<List<string?>?> ExecuteManyAsync(string host, int port, IEnumerable<string> commands,
        IProgress<int>? progress = null)
    {
        var results = new List<string?>();
        try
        {
            using var client = await ConnectAsync(host, port);
            var stream = client.GetStream();
            stream.WriteTimeout = READ_TIMEOUT_MS;

            int done = 0;
            foreach (var command in commands)
            {
                await SendAsync(stream, command);
                results.Add(await ReadResponseAsync(client, stream));
                progress?.Report(++done);
            }
            return results;
        }
        catch (Exception ex)
        {
            _logger.Warning($"EchoPort batch failed after {results.Count} command(s): {ex.Message}");
            // Partial results are still useful to the caller
            return results.Count > 0 ? results : null;
        }
    }

    private static async Task SendAsync(NetworkStream stream, string command)
    {
        byte[] cmd = Encoding.UTF8.GetBytes(command + "\r\n");
        await stream.WriteAsync(cmd);
        await stream.FlushAsync();
    }

    /// <summary>
    /// Reads one command's response. Polls Available instead of cancelling a
    /// pending ReadAsync — a cancelled read can leave the socket unusable, which
    /// would break the next command on a shared connection.
    /// </summary>
    private static async Task<string> ReadResponseAsync(TcpClient client, NetworkStream stream)
    {
        var sb    = new StringBuilder();
        var buf   = new byte[4096];
        long start    = Environment.TickCount64;
        long lastData = start;

        while (true)
        {
            if (client.Available > 0)
            {
                int n = await stream.ReadAsync(buf);   // data is buffered — won't block
                if (n <= 0) break;                     // server closed the connection
                sb.Append(Encoding.UTF8.GetString(buf, 0, n));
                lastData = Environment.TickCount64;
                continue;
            }

            long now = Environment.TickCount64;
            if (sb.Length > 0 && now - lastData >= IDLE_TIMEOUT_MS) break; // response complete
            if (sb.Length == 0 && now - start   >= READ_TIMEOUT_MS) break; // no reply at all
            if (now - start >= READ_TIMEOUT_MS * 2) break;                 // hard cap

            await Task.Delay(25);
        }

        return sb.ToString();
    }

    // List_OnlinePlayers (alias: lp) — players currently connected.
    // Returns null if the connection failed (server not running / port unreachable).
    public async Task<List<PlayerInfo>?> GetPlayersAsync(string host, int port)
    {
        var response = await ExecuteAsync(host, port, "lp");
        return response == null ? null : ParsePlayers(response);
    }

    // SayToSystemChannel (alias: say) — system chat broadcast to all players
    public Task<bool> BroadcastAsync(string host, int port, string message) =>
        ExecuteAndCheck(host, port, $"say {message}");

    // SaveWorld 0 — flush world to in-memory DB (note: SaveWorld 1 is broken per wiki)
    public Task<bool> SaveWorldAsync(string host, int port) =>
        ExecuteAndCheck(host, port, "SaveWorld 0");

    // SaveAndExit N (alias: shutdown N) — save + graceful shutdown after N seconds
    // Note: passing 0 sets a 300-second timer; use a small value like 10 for immediate stop
    public Task<bool> ShutdownAsync(string host, int port, int delaySeconds = 10) =>
        ExecuteAndCheck(host, port, $"shutdown {delaySeconds}");

    // StopCloseServer (alias: cc) — cancel a pending SaveAndExit countdown
    public Task<bool> CancelShutdownAsync(string host, int port) =>
        ExecuteAndCheck(host, port, "cc");

    // Update_ServerPermissionList 1 1 steamID — add to ban list (permission list type 1)
    public Task<bool> BanPlayerAsync(string host, int port, string steamId) =>
        ExecuteAndCheck(host, port, $"usp 1 1 {steamId}");

    // Update_ServerPermissionList 1 0 steamID — remove from ban list
    public Task<bool> UnbanPlayerAsync(string host, int port, string steamId) =>
        ExecuteAndCheck(host, port, $"usp 1 0 {steamId}");

    // Update_ServerPermissionList 4 1 steamID — add to mute list (type 4)
    public Task<bool> MutePlayerAsync(string host, int port, string steamId) =>
        ExecuteAndCheck(host, port, $"usp 4 1 {steamId}");

    // Update_ServerPermissionList 4 0 steamID — remove from mute list
    public Task<bool> UnmutePlayerAsync(string host, int port, string steamId) =>
        ExecuteAndCheck(host, port, $"usp 4 0 {steamId}");

    // No dedicated kick command over EchoPort. Ban immediately kicks the player regardless of
    // whether the ban list is enabled, then we immediately unban so they can rejoin.
    public async Task<bool> KickPlayerAsync(string host, int port, string steamId)
    {
        var ban = await ExecuteAsync(host, port, $"usp 1 1 {steamId}"); // adds to ban list → instant kick
        if (ban == null) return false;

        await Task.Delay(500);
        var unban = await ExecuteAsync(host, port, $"usp 1 0 {steamId}"); // removes from ban list → can rejoin
        if (unban == null)
        {
            // The ban went through but the unban didn't — retry once so the
            // player isn't silently left permanently banned.
            _logger.Warning($"Kick: unban of {steamId} failed, retrying...");
            await Task.Delay(1000);
            unban = await ExecuteAsync(host, port, $"usp 1 0 {steamId}");
        }
        return unban != null;
    }

    // BackupDatabaseByHour (alias: bkh) — write world save to timestamped file on disk
    // Run SaveWorld 0 first to ensure recent state
    public async Task<bool> BackupDatabaseAsync(string host, int port)
    {
        await ExecuteAsync(host, port, "SaveWorld 0");
        return await ExecuteAndCheck(host, port, "bkh");
    }

    // ── Helpers ──────────────────────────────────────────────────────

    private async Task<bool> ExecuteAndCheck(string host, int port, string cmd)
    {
        var result = await ExecuteAsync(host, port, cmd);
        return result != null;
    }

    private async Task<TcpClient> ConnectAsync(string host, int port)
    {
        var client      = new TcpClient { NoDelay = true };
        var connectTask = client.ConnectAsync(host, port);
        if (await Task.WhenAny(connectTask, Task.Delay(CONNECT_TIMEOUT_MS)) != connectTask)
        {
            client.Dispose();
            throw new TimeoutException($"EchoPort connection to {host}:{port} timed out.");
        }
        if (connectTask.IsFaulted)
        {
            client.Dispose();
            throw connectTask.Exception?.InnerException ?? new IOException("EchoPort connection failed.");
        }
        return client;
    }

    private static List<PlayerInfo> ParsePlayers(string response)
    {
        var players = new List<PlayerInfo>();
        if (string.IsNullOrWhiteSpace(response)) return players;

        // Server returns a pipe-delimited table:
        // | Account | PlayerName | PawnID | Position |
        // | 76561197993213308 | 'Sibercat' | 8XRQ... | V(X=...) |
        bool foundHeader = false;
        foreach (var line in response.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmed = line.Trim();
            if (!trimmed.StartsWith('|')) continue;

            var cols = trimmed.Split('|', StringSplitOptions.RemoveEmptyEntries);
            if (cols.Length < 2) continue;

            // Header row contains "Account" — skip it
            if (cols[0].Trim().Equals("Account", StringComparison.OrdinalIgnoreCase))
            {
                foundHeader = true;
                continue;
            }

            if (!foundHeader) continue;

            // cols[0]=Account(SteamID), cols[1]=PlayerName, cols[2]=PawnID, cols[3]=Position
            string steamId = cols[0].Trim();
            string name    = cols.Length > 1 ? cols[1].Trim().Trim('\'') : steamId;

            if (Regex.IsMatch(steamId, @"^\d{10,}$"))
                players.Add(new PlayerInfo(name, steamId));
        }

        return players;
    }
}
