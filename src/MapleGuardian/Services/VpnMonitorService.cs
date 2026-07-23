using System.Net.NetworkInformation;
using MapleGuardian.Models;
using Timer = System.Threading.Timer;

namespace MapleGuardian.Services;

/// <summary>
/// Event-driven VPN monitoring service.
/// Replaces the PowerShell polling loop with NetworkChange events + backup timer.
/// CPU usage ~0% when VPN is stable.
/// </summary>
public class VpnMonitorService : IDisposable
{
    private readonly AppConfig _config;
    private readonly LogService _log;
    private Timer? _backupTimer;
    private VpnStatus _currentStatus = VpnStatus.Unknown;
    private readonly object _statusLock = new();
    private bool _disposed;

    /// <summary>Fired when VPN status changes</summary>
    public event EventHandler<VpnStatusChangedEventArgs>? StatusChanged;

    public VpnStatus CurrentStatus
    {
        get { lock (_statusLock) return _currentStatus; }
    }

    public string AdapterDescription { get; private set; } = string.Empty;

    public VpnMonitorService(AppConfig config, LogService log)
    {
        _config = config;
        _log = log;
    }

    /// <summary>
    /// Start monitoring VPN status using events + backup timer
    /// </summary>
    public void Start()
    {
        _log.Info("VpnMonitor", $"Starting VPN monitoring for adapter: {_config.VpnAdapterName}");

        // Subscribe to network change events (event-driven, no polling)
        NetworkChange.NetworkAddressChanged += OnNetworkAddressChanged;
        NetworkChange.NetworkAvailabilityChanged += OnNetworkAvailabilityChanged;

        // High-frequency backup timer (500ms) to ensure sub-second response alongside OS events
        _backupTimer = new Timer(
            _ => CheckVpnStatus(),
            null,
            TimeSpan.Zero,
            TimeSpan.FromMilliseconds(500));

        _log.Info("VpnMonitor", "VPN monitoring started");
    }

    /// <summary>
    /// Stop monitoring
    /// </summary>
    public void Stop()
    {
        NetworkChange.NetworkAddressChanged -= OnNetworkAddressChanged;
        NetworkChange.NetworkAvailabilityChanged -= OnNetworkAvailabilityChanged;
        _backupTimer?.Change(Timeout.Infinite, Timeout.Infinite);
        _backupTimer?.Dispose();
        _backupTimer = null;
        _log.Info("VpnMonitor", "VPN monitoring stopped");
    }

    private void OnNetworkAddressChanged(object? sender, EventArgs e)
    {
        _log.Info("VpnMonitor", "Network address changed event detected");
        CheckVpnStatus();
    }

    private void OnNetworkAvailabilityChanged(object? sender, NetworkAvailabilityEventArgs e)
    {
        _log.Info("VpnMonitor", $"Network availability changed: {(e.IsAvailable ? "Available" : "Unavailable")}");
        CheckVpnStatus();
    }

    /// <summary>
    /// Check VPN adapter status and raise event if changed
    /// </summary>
    public void CheckVpnStatus()
    {
        if (_disposed) return;

        try
        {
            var interfaces = NetworkInterface.GetAllNetworkInterfaces();
            NetworkInterface? vpnAdapter = null;

            foreach (var ni in interfaces)
            {
                // Match by adapter name (same as PowerShell: Get-NetAdapter -Name "VPN - VPN Client")
                if (ni.Name.Equals(_config.VpnAdapterName, StringComparison.OrdinalIgnoreCase) ||
                    ni.Description.Contains("SoftEther", StringComparison.OrdinalIgnoreCase))
                {
                    vpnAdapter = ni;
                    break;
                }
            }

            VpnStatus newStatus;
            if (vpnAdapter == null)
            {
                newStatus = VpnStatus.Disconnected;
                AdapterDescription = "Not found";
            }
            else if (vpnAdapter.OperationalStatus == OperationalStatus.Up)
            {
                newStatus = VpnStatus.Connected;
                AdapterDescription = vpnAdapter.Description;
            }
            else
            {
                newStatus = VpnStatus.Disconnected;
                AdapterDescription = vpnAdapter.Description;
            }

            VpnStatus oldStatus;
            lock (_statusLock)
            {
                oldStatus = _currentStatus;
                // If currently reconnecting and network is still down, stay in Reconnecting state
                if (oldStatus == VpnStatus.Reconnecting && newStatus == VpnStatus.Disconnected)
                    return;

                if (oldStatus == newStatus) return; // No change
                _currentStatus = newStatus;
            }

            _log.Info("VpnMonitor", $"VPN status changed: {oldStatus} → {newStatus}");
            StatusChanged?.Invoke(this, new VpnStatusChangedEventArgs(oldStatus, newStatus));
        }
        catch (Exception ex)
        {
            _log.Error("VpnMonitor", "Error checking VPN status", ex);
        }
    }

    /// <summary>
    /// Manually set status to Reconnecting (called by reconnect service)
    /// </summary>
    public void SetReconnecting()
    {
        lock (_statusLock)
        {
            var old = _currentStatus;
            _currentStatus = VpnStatus.Reconnecting;
            StatusChanged?.Invoke(this, new VpnStatusChangedEventArgs(old, VpnStatus.Reconnecting));
        }
    }

    /// <summary>
    /// Reset status to Disconnected (called when reconnect completes unsuccessfully)
    /// </summary>
    public void SetDisconnected()
    {
        lock (_statusLock)
        {
            var old = _currentStatus;
            _currentStatus = VpnStatus.Disconnected;
            StatusChanged?.Invoke(this, new VpnStatusChangedEventArgs(old, VpnStatus.Disconnected));
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

/// <summary>
/// Event args for VPN status changes
/// </summary>
public class VpnStatusChangedEventArgs : EventArgs
{
    public VpnStatus OldStatus { get; }
    public VpnStatus NewStatus { get; }

    public VpnStatusChangedEventArgs(VpnStatus oldStatus, VpnStatus newStatus)
    {
        OldStatus = oldStatus;
        NewStatus = newStatus;
    }
}
