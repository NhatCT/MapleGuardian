namespace MapleGuardian.Models;

/// <summary>
/// Represents a single log entry for the log viewer
/// </summary>
public class LogEntry
{
    public DateTime Timestamp { get; set; }
    public string Level { get; set; } = "Info";
    public string Message { get; set; } = string.Empty;
    public string? Details { get; set; }

    public LogEntry() { }

    public LogEntry(string level, string message, string? details = null)
    {
        Timestamp = DateTime.Now;
        Level = level;
        Message = message;
        Details = details;
    }

    public override string ToString()
        => $"[{Timestamp:yyyy-MM-dd HH:mm:ss}] [{Level}] {Message}";
}
