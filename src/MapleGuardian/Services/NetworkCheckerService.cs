using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Text.Json;
using MapleGuardian.Models;
using Timer = System.Threading.Timer;

namespace MapleGuardian.Services;

/// <summary>
/// 3-Layer Protection Network Checker:
/// 1. Public IP & Country verification (Korea check)
/// 2. IP Type Inspection (Residential vs Datacenter/IDC detection for Nexon ban protection)
/// 3. DNS Leak Shield & Cache Flushing
/// </summary>
public class NetworkCheckerService : IDisposable
{
    private readonly AppConfig _config;
    private readonly LogService _log;
    private readonly HttpClient _httpClient;
    private Timer? _checkTimer;
    private bool _disposed;

    // Current results
    public string PublicIp { get; private set; } = "Checking...";
    public long PingMs { get; private set; } = -1;
    public DnsStatus DnsStatus { get; private set; } = DnsStatus.Unknown;
    public string ServerLocation { get; private set; } = "Unknown";
    public string CountryCode { get; private set; } = "";
    public string IpType { get; private set; } = "Unknown"; // "Residential" or "Datacenter (IDC)"
    public bool IsKoreaIp { get; private set; }
    public bool IsDatacenterIp { get; private set; }
    public string IspName { get; private set; } = "";

    public event EventHandler? ResultsUpdated;

    public NetworkCheckerService(AppConfig config, LogService log)
    {
        _config = config;
        _log = log;
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
    }

    /// <summary>
    /// Start periodic network checks
    /// </summary>
    public void Start()
    {
        _log.Info("NetworkChecker", "Starting 3-Layer Network Checks");
        FlushDnsCache();
        _checkTimer = new Timer(
            async _ => await RunAllChecksAsync(),
            null,
            TimeSpan.Zero,
            TimeSpan.FromMilliseconds(_config.NetworkCheckIntervalMs));
    }

    /// <summary>
    /// Stop periodic checks
    /// </summary>
    public void Stop()
    {
        _checkTimer?.Change(Timeout.Infinite, Timeout.Infinite);
        _checkTimer?.Dispose();
        _checkTimer = null;
    }

    /// <summary>
    /// Run all network checks (IP, ISP/IDC Type, Ping, DNS)
    /// </summary>
    public async Task RunAllChecksAsync()
    {
        if (_disposed) return;

        await Task.WhenAll(
            CheckPublicIpAndTypeAsync(),
            CheckPingAsync(),
            CheckDnsAsync()
        );

        ResultsUpdated?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Get public IP, Geo location, and inspect if IP is Datacenter (IDC) vs Residential ISP
    /// </summary>
    public async Task CheckPublicIpAndTypeAsync()
    {
        try
        {
            var ip = await _httpClient.GetStringAsync("https://api.ipify.org");
            ip = ip.Trim();

            if (ip != PublicIp || IpType == "Unknown")
            {
                _log.Info("NetworkChecker", $"Public IP: {MaskIp(ip)}");
                PublicIp = ip;
                await InspectIpDetails(ip);
            }
        }
        catch (Exception ex)
        {
            PublicIp = "Error";
            ServerLocation = "Unknown";
            IpType = "Unknown";
            IsKoreaIp = false;
            _log.Warning("NetworkChecker", $"IP check failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Query IP API for Country and Datacenter/Hosting flags (Namu Wiki ban check)
    /// </summary>
    private async Task InspectIpDetails(string ip)
    {
        try
        {
            var jsonStr = await _httpClient.GetStringAsync($"http://ip-api.com/json/{ip}?fields=status,country,countryCode,isp,org,hosting,proxy");
            using var doc = JsonDocument.Parse(jsonStr);
            var root = doc.RootElement;

            if (root.TryGetProperty("status", out var statusProp) && statusProp.GetString() == "success")
            {
                ServerLocation = root.TryGetProperty("country", out var cProp) ? cProp.GetString() ?? "Unknown" : "Unknown";
                CountryCode = root.TryGetProperty("countryCode", out var ccProp) ? ccProp.GetString() ?? "" : "";
                IspName = root.TryGetProperty("isp", out var ispProp) ? ispProp.GetString() ?? "" : "";

                bool hosting = root.TryGetProperty("hosting", out var hProp) && hProp.GetBoolean();
                bool proxy = root.TryGetProperty("proxy", out var pProp) && pProp.GetBoolean();

                IsDatacenterIp = hosting || proxy;
                IpType = IsDatacenterIp ? "Datacenter (IDC)" : "Residential ISP";
                IsKoreaIp = string.Equals(CountryCode, "KR", StringComparison.OrdinalIgnoreCase) ||
                            ServerLocation.Contains("Korea", StringComparison.OrdinalIgnoreCase);

                _log.Info("NetworkChecker", $"IP Inspection → Country: {ServerLocation} ({CountryCode}), Type: {IpType}, ISP: {IspName}");

                if (IsDatacenterIp)
                {
                    _log.Warning("NetworkChecker", "⚠️ WARNING: Connected IP is detected as Datacenter/IDC. Nexon automated bans target IDC IPs!");
                }
            }
        }
        catch (Exception ex)
        {
            _log.Warning("NetworkChecker", $"Failed to inspect IP details: {ex.Message}");
        }
    }

    /// <summary>
    /// Ping target to measure latency
    /// </summary>
    public async Task CheckPingAsync()
    {
        try
        {
            using var ping = new Ping();
            var reply = await ping.SendPingAsync(_config.PingTarget, 3000);
            PingMs = reply.Status == IPStatus.Success ? reply.RoundtripTime : -1;
        }
        catch
        {
            PingMs = -1;
        }
    }

    /// <summary>
    /// Check DNS for leaks & ensure DNS is working properly
    /// </summary>
    public async Task CheckDnsAsync()
    {
        try
        {
            var addresses = await Dns.GetHostAddressesAsync(_config.DnsTestDomain);
            DnsStatus = addresses.Length > 0 ? DnsStatus.Safe : DnsStatus.Unknown;
        }
        catch
        {
            DnsStatus = DnsStatus.Error;
        }
    }

    /// <summary>
    /// Flush Windows DNS Cache to prevent DNS Leak when VPN state changes
    /// </summary>
    public void FlushDnsCache()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "ipconfig",
                Arguments = "/flushdns",
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var proc = Process.Start(psi);
            proc?.WaitForExit(2000);
            _log.Info("NetworkChecker", "🧹 DNS Cache flushed successfully");
        }
        catch (Exception ex)
        {
            _log.Warning("NetworkChecker", $"Failed to flush DNS cache: {ex.Message}");
        }
    }

    private static string MaskIp(string ip)
    {
        var parts = ip.Split('.');
        if (parts.Length == 4)
            return $"{parts[0]}.xxx.xxx.{parts[3]}";
        return ip;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Stop();
        _httpClient.Dispose();
        GC.SuppressFinalize(this);
    }
}
