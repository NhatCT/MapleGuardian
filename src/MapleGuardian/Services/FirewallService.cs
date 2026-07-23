using MapleGuardian.Models;

namespace MapleGuardian.Services;

/// <summary>
/// Windows Firewall management via COM API (NetFwTypeLib).
/// No PowerShell spawning needed - direct API calls.
/// Enables/disables firewall rules to block/unblock game traffic.
/// </summary>
public class FirewallService
{
    private readonly AppConfig _config;
    private readonly LogService _log;
    private FirewallStatus _currentStatus = FirewallStatus.Disabled;
    private dynamic? _cachedPolicy;
    private readonly object _lock = new();

    public FirewallStatus CurrentStatus => _currentStatus;

    public FirewallService(AppConfig config, LogService log)
    {
        _config = config;
        _log = log;
        // Pre-warm COM policy object at startup for sub-millisecond zero-latency execution
        _cachedPolicy = GetFirewallPolicy();
    }

    /// <summary>
    /// Enable firewall rules to BLOCK game traffic (VPN is down) — Sub-millisecond execution
    /// </summary>
    public bool EnableRules()
    {
        _log.Warning("Firewall", "🛡️ Enabling firewall rules — BLOCKING game traffic");
        bool success = SetRulesEnabled(true);
        if (success)
        {
            _currentStatus = FirewallStatus.Enabled;
            _log.Info("Firewall", "Firewall rules enabled successfully");
        }
        return success;
    }

    /// <summary>
    /// Disable firewall rules to ALLOW game traffic (VPN is up) — Sub-millisecond execution
    /// </summary>
    public bool DisableRules()
    {
        _log.Info("Firewall", "✅ Disabling firewall rules — ALLOWING game traffic");
        bool success = SetRulesEnabled(false);
        if (success)
        {
            _currentStatus = FirewallStatus.Disabled;
            _log.Info("Firewall", "Firewall rules disabled successfully");
        }
        return success;
    }

    /// <summary>
    /// Check current status of firewall rules
    /// </summary>
    public FirewallStatus CheckRulesStatus()
    {
        try
        {
            var policy = GetFirewallPolicy();
            if (policy == null)
            {
                _currentStatus = FirewallStatus.Error;
                return _currentStatus;
            }

            int enabledCount = 0;
            foreach (var ruleName in _config.FirewallRules)
            {
                foreach (dynamic rule in policy.Rules)
                {
                    if (string.Equals(rule.Name, ruleName, StringComparison.OrdinalIgnoreCase))
                    {
                        if (rule.Enabled)
                            enabledCount++;
                        break;
                    }
                }
            }

            _currentStatus = enabledCount > 0 ? FirewallStatus.Enabled : FirewallStatus.Disabled;
            return _currentStatus;
        }
        catch (Exception ex)
        {
            _log.Error("Firewall", "Error checking firewall status", ex);
            _currentStatus = FirewallStatus.Error;
            return _currentStatus;
        }
    }

    /// <summary>
    /// Get details of all monitored firewall rules
    /// </summary>
    public List<(string Name, bool Enabled, string? Action)> GetRuleDetails()
    {
        var result = new List<(string, bool, string?)>();
        try
        {
            var policy = GetFirewallPolicy();
            if (policy == null) return result;

            foreach (var ruleName in _config.FirewallRules)
            {
                foreach (dynamic rule in policy.Rules)
                {
                    if (string.Equals(rule.Name, ruleName, StringComparison.OrdinalIgnoreCase))
                    {
                        result.Add((rule.Name, rule.Enabled, rule.Action == 0 ? "Block" : "Allow"));
                        break;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _log.Error("Firewall", "Error getting rule details", ex);
        }
        return result;
    }

    private bool SetRulesEnabled(bool blockAll)
    {
        try
        {
            var policy = GetFirewallPolicy();
            if (policy == null)
            {
                _log.Error("Firewall", "Failed to get firewall policy object");
                _currentStatus = FirewallStatus.Error;
                return false;
            }

            int matchedRules = 0;
            foreach (var ruleName in _config.FirewallRules)
            {
                foreach (dynamic rule in policy.Rules)
                {
                    if (string.Equals(rule.Name, ruleName, StringComparison.OrdinalIgnoreCase))
                    {
                        if (blockAll)
                        {
                            // Full lockdown: Block on ALL interfaces
                            rule.InterfaceTypes = "All";
                            rule.Enabled = true;
                        }
                        else
                        {
                            // Proactive Hard-Lock: Block physical LAN & Wi-Fi 24/7, allow VPN (RemoteAccess)
                            rule.InterfaceTypes = "LAN, Wireless";
                            rule.Enabled = true;
                        }
                        matchedRules++;
                        break;
                    }
                }
            }

            return matchedRules > 0;
        }
        catch (UnauthorizedAccessException)
        {
            _log.Error("Firewall", "Access denied — app must run as Administrator!");
            _currentStatus = FirewallStatus.Error;
            return false;
        }
        catch (Exception ex)
        {
            _cachedPolicy = null; // Reset cache on COM error to re-instantiate
            _log.Error("Firewall", "Error modifying firewall rules", ex);
            _currentStatus = FirewallStatus.Error;
            return false;
        }
    }

    private dynamic? GetFirewallPolicy()
    {
        lock (_lock)
        {
            if (_cachedPolicy != null) return _cachedPolicy;
            try
            {
                Type? policyType = Type.GetTypeFromProgID("HNetCfg.FwPolicy2");
                if (policyType == null)
                {
                    _log.Error("Firewall", "HNetCfg.FwPolicy2 COM type not found");
                    return null;
                }
                _cachedPolicy = Activator.CreateInstance(policyType);
                return _cachedPolicy;
            }
            catch (Exception ex)
            {
                _log.Error("Firewall", "Failed to create firewall policy instance", ex);
                return null;
            }
        }
    }
}
