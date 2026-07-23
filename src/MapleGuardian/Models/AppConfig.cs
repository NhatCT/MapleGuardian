namespace MapleGuardian.Models;

/// <summary>
/// Strongly-typed configuration loaded from appsettings.json
/// </summary>
public class AppConfig
{
    public string VpnAdapterName { get; set; } = "VPN - VPN Client";
    public string SoftEtherPath { get; set; } = @"C:\Program Files\SoftEther VPN Client\vpncmd.exe";
    public string SoftEtherAccountName { get; set; } = "VPN";
    public string[] FirewallRules { get; set; } = ["MSW Block", "NGM64 Block", "NexonLink Block"];
    public string[] GameProcesses { get; set; } = ["MaplePlanet"];
    public string PingTarget { get; set; } = "8.8.8.8";
    public string DnsTestDomain { get; set; } = "google.com";
    public int ReconnectDelaySeconds { get; set; } = 5;
    public int MaxReconnectAttempts { get; set; } = 10;
    public int CheckIntervalMs { get; set; } = 3000;
    public int NetworkCheckIntervalMs { get; set; } = 10000;
    public int LogRetentionDays { get; set; } = 30;
    public string UpdateUrl { get; set; } = "https://raw.githubusercontent.com/MapleGuardian/releases/main/version.json";
    public bool EnableAutoUpdate { get; set; } = true;
}
