using Serilog;
using MapleGuardian.Models;
using Path = System.IO.Path;
using File = System.IO.File;
using Directory = System.IO.Directory;

namespace MapleGuardian.Services;

/// <summary>
/// Centralized logging service using Serilog with rolling file support.
/// All services use this for consistent log output.
/// </summary>
public class LogService : IDisposable
{
    private readonly Serilog.Core.Logger _logger;
    private readonly string _logDirectory;
    private readonly int _retentionDays;
    private readonly object _recentLock = new();
    private readonly List<LogEntry> _recentEntries = new();
    private const int MaxRecentEntries = 500;

    public event EventHandler<LogEntry>? LogAdded;

    public LogService(AppConfig config)
    {
        _retentionDays = config.LogRetentionDays;
        _logDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
        Directory.CreateDirectory(_logDirectory);

        _logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.File(
                Path.Combine(_logDirectory, "maple-guardian-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: _retentionDays,
                outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff}] [{Level:u3}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();

        Info("LogService", "Logging initialized");
        CleanupOldLogs();
    }

    public void Info(string source, string message)
    {
        var entry = new LogEntry("INF", $"[{source}] {message}");
        _logger.Information("[{Source}] {Message}", source, message);
        AddRecentEntry(entry);
    }

    public void Warning(string source, string message)
    {
        var entry = new LogEntry("WRN", $"[{source}] {message}");
        _logger.Warning("[{Source}] {Message}", source, message);
        AddRecentEntry(entry);
    }

    public void Error(string source, string message, Exception? ex = null)
    {
        var entry = new LogEntry("ERR", $"[{source}] {message}", ex?.ToString());
        if (ex != null)
            _logger.Error(ex, "[{Source}] {Message}", source, message);
        else
            _logger.Error("[{Source}] {Message}", source, message);
        AddRecentEntry(entry);
    }

    private void AddRecentEntry(LogEntry entry)
    {
        lock (_recentLock)
        {
            _recentEntries.Add(entry);
            if (_recentEntries.Count > MaxRecentEntries)
                _recentEntries.RemoveAt(0);
        }
        LogAdded?.Invoke(this, entry);
    }

    public List<LogEntry> GetRecentEntries()
    {
        lock (_recentLock)
        {
            return new List<LogEntry>(_recentEntries);
        }
    }

    public string GetLogDirectory() => _logDirectory;

    public void ExportLog(string outputPath)
    {
        lock (_recentLock)
        {
            var lines = _recentEntries.Select(e => e.ToString());
            File.WriteAllLines(outputPath, lines);
        }
    }

    private void CleanupOldLogs()
    {
        try
        {
            var cutoff = DateTime.Now.AddDays(-_retentionDays);
            foreach (var file in Directory.GetFiles(_logDirectory, "maple-guardian-*.log"))
            {
                if (File.GetCreationTime(file) < cutoff)
                {
                    File.Delete(file);
                    _logger.Information("Deleted old log file: {File}", Path.GetFileName(file));
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Failed to cleanup old logs");
        }
    }

    public void Dispose()
    {
        _logger.Dispose();
        GC.SuppressFinalize(this);
    }
}
