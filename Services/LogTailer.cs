namespace SoulMaskServerManager.Services;

/// <summary>
/// Follows a growing log file by path, surviving the truncation and rotation
/// Unreal performs when the server restarts.
///
/// Holding one long-lived FileStream does NOT work here: a Windows file handle
/// follows the file, not the path, so once Unreal renames WS.log to its backup
/// name the reader is stranded on the old file and never sees another line.
/// This reopens by path each poll and re-syncs whenever the file shrinks or is
/// replaced.
/// </summary>
public sealed class LogTailer
{
    private const int MaxCarry = 8192;

    private readonly string _path;
    private long _position;
    private DateTime? _stamp;
    private Decoder _decoder = Encoding.UTF8.GetDecoder();
    private string _carry = "";

    /// <summary>Set when the file was replaced or truncated since the last read.</summary>
    public bool RotationDetected { get; private set; }

    /// <param name="skipExistingContent">
    /// True to ignore whatever is already in the file — needed on server start so
    /// the *previous* run's "loaded" marker isn't mistaken for this run's.
    /// </param>
    public LogTailer(string path, bool skipExistingContent)
    {
        _path = path;
        try
        {
            var fi = new FileInfo(path);
            if (fi.Exists && skipExistingContent)
            {
                _position = fi.Length;
                _stamp    = fi.CreationTimeUtc;
            }
        }
        catch { /* treat as not-yet-existing */ }
    }

    /// <summary>
    /// Returns text appended since the last call ("" when there is nothing new).
    /// A trailing partial line is held back and prepended to the next result, so
    /// callers can match strings that straddle a read boundary.
    /// </summary>
    public string ReadNew()
    {
        RotationDetected = false;

        FileInfo fi;
        try
        {
            fi = new FileInfo(_path);
            if (!fi.Exists) return "";
        }
        catch { return ""; }

        try
        {
            if (_stamp == null)
            {
                // First sighting after construction — read from the top.
                _stamp = fi.CreationTimeUtc;
                Reset();
            }
            else if (fi.Length < _position || fi.CreationTimeUtc != _stamp)
            {
                // Shrunk (truncated) or a different file at the same path (rotated).
                // Length is the primary signal: NTFS tunneling can preserve the
                // creation time of a file deleted and recreated moments later.
                _stamp = fi.CreationTimeUtc;
                Reset();
                RotationDetected = true;
            }

            if (fi.Length <= _position) return "";

            using var fs = new FileStream(_path, FileMode.Open, FileAccess.Read,
                                          FileShare.ReadWrite | FileShare.Delete);
            fs.Seek(_position, SeekOrigin.Begin);

            var sb  = new StringBuilder(_carry);
            var buf = new byte[16 * 1024];
            int n;
            while ((n = fs.Read(buf, 0, buf.Length)) > 0)
            {
                var chars = new char[_decoder.GetCharCount(buf, 0, n)];
                int c = _decoder.GetChars(buf, 0, n, chars, 0);
                sb.Append(chars, 0, c);
            }
            _position = fs.Position;

            string text = sb.ToString();

            // Hold back the trailing partial line for the next call
            int nl = text.LastIndexOf('\n');
            _carry = nl >= 0 ? text[(nl + 1)..] : text;
            if (_carry.Length > MaxCarry) _carry = _carry[^MaxCarry..];

            return text;
        }
        catch
        {
            // Log momentarily locked or mid-rotation — try again next poll
            return "";
        }
    }

    private void Reset()
    {
        _position = 0;
        _carry    = "";
        _decoder  = Encoding.UTF8.GetDecoder();
    }
}
