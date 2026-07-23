using System.Diagnostics;
using MapleGuardian.Models;
using Timer = System.Threading.Timer;

namespace MapleGuardian.Services;

/// <summary>
/// Monitors game processes (e.g., MaplePlanet.exe) to determine if the game is running.
/// Uses periodic polling since WMI events can be unreliable for process monitoring.
/// </summary>
public class ProcessMonitorService : IDisposable
{
    private readonly AppConfig _config;
    private readonly LogService _log;
    private Timer? _checkTimer;
    private GameStatus _currentStatus = GameStatus.Stopped;
    private bool _disposed;

    public GameStatus CurrentStatus => _currentStatus;

    public event EventHandler<GameStatus>? StatusChanged;

    public ProcessMonitorService(AppConfig config, LogService log)
    {
        _config = config;
        _log = log;
    }

    /// <summary>
    /// Start monitoring game processes
    /// </summary>
    public void Start()
    {
        _log.Info("ProcessMonitor", $"Monitoring processes: {string.Join(", ", _config.GameProcesses)}");
        _checkTimer = new Timer(
            _ => CheckProcesses(),
            null,
            TimeSpan.Zero,
            TimeSpan.FromSeconds(5));
    }

    /// <summary>
    /// Stop monitoring
    /// </summary>
    public void Stop()
    {
        _checkTimer?.Change(Timeout.Infinite, Timeout.Infinite);
        _checkTimer?.Dispose();
        _checkTimer = null;
    }

    /// <summary>
    /// Check if any monitored game process is running
    /// </summary>
    public void CheckProcesses()
    {
        if (_disposed) return;

        try
        {
            bool isRunning = false;
            foreach (var processName in _config.GameProcesses)
            {
                var processes = Process.GetProcessesByName(processName);
                if (processes.Length > 0)
                {
                    isRunning = true;
                    foreach (var p in processes) p.Dispose();
                    break;
                }
                foreach (var p in processes) p.Dispose();
            }

            var newStatus = isRunning ? GameStatus.Running : GameStatus.Stopped;
            if (newStatus != _currentStatus)
            {
                var oldStatus = _currentStatus;
                _currentStatus = newStatus;
                _log.Info("ProcessMonitor", $"Game status changed: {oldStatus} → {newStatus}");
                StatusChanged?.Invoke(this, newStatus);
            }
        }
        catch (Exception ex)
        {
            _log.Error("ProcessMonitor", "Error checking processes", ex);
        }
    }

    /// <summary>
    /// Mark game as blocked (when firewall rules are enabled)
    /// </summary>
    public void SetBlocked()
    {
        if (_currentStatus == GameStatus.Running)
        {
            _currentStatus = GameStatus.Blocked;
            StatusChanged?.Invoke(this, GameStatus.Blocked);
        }
    }

    /// <summary>
    /// Clear blocked status
    /// </summary>
    public void ClearBlocked()
    {
        if (_currentStatus == GameStatus.Blocked)
        {
            CheckProcesses(); // Re-check actual status
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Stop();
        GC.SuppressFinalize(this);
    }
}
