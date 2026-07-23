namespace MapleGuardian.Models;

/// <summary>
/// VPN connection status
/// </summary>
public enum VpnStatus
{
    Connected,
    Disconnected,
    Reconnecting,
    Unknown
}

/// <summary>
/// Windows Firewall blocking status
/// </summary>
public enum FirewallStatus
{
    /// <summary>Firewall rules are enabled (game is BLOCKED)</summary>
    Enabled,
    /// <summary>Firewall rules are disabled (game is ALLOWED)</summary>
    Disabled,
    /// <summary>Error accessing firewall</summary>
    Error
}

/// <summary>
/// Game process status
/// </summary>
public enum GameStatus
{
    Running,
    Stopped,
    Blocked
}

/// <summary>
/// DNS leak check status
/// </summary>
public enum DnsStatus
{
    Safe,
    Leaked,
    Unknown,
    Error
}
