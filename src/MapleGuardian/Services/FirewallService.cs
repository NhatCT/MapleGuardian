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

    public FirewallStatus CurrentStatus => _currentStatus;

    public FirewallService(AppConfig config, LogService log)
    {
        _config = config;
        _log = log;
    }

    /// <summary>
    /// Enable firewall rules to BLOCK game traffic (VPN is down)
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
    /// Disable firewall rules to ALLOW game traffic (VPN is up)
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
            int totalRules = _config.FirewallRules.Length;

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

    private bool SetRulesEnabled(bool enabled)
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
                        rule.Enabled = enabled;
                        matchedRules++;
                        _log.Info("Firewall", $"  Rule '{ruleName}' → {(enabled ? "ENABLED" : "DISABLED")}");
                        break;
                    }
                }
            }

            if (matchedRules == 0)
            {
                _log.Warning("Firewall", "No matching firewall rules found! Make sure rules exist in Windows Firewall.");
                return false;
            }

            if (matchedRules < _config.FirewallRules.Length)
            {
                _log.Warning("Firewall", $"Only {matchedRules}/{_config.FirewallRules.Length} rules found");
            }

            return true;
        }
        catch (UnauthorizedAccessException)
        {
            _log.Error("Firewall", "Access denied — app must run as Administrator!");
            _currentStatus = FirewallStatus.Error;
            return false;
        }
        catch (Exception ex)
        {
            _log.Error("Firewall", "Error modifying firewall rules", ex);
            _currentStatus = FirewallStatus.Error;
            return false;
        }
    }

    private dynamic? GetFirewallPolicy()
    {
        try
        {
            Type? policyType = Type.GetTypeFromProgID("HNetCfg.FwPolicy2");
            if (policyType == null)
            {
                _log.Error("Firewall", "HNetCfg.FwPolicy2 COM type not found");
                return null;
            }
            return Activator.CreateInstance(policyType);
        }
        catch (Exception ex)
        {
            _log.Error("Firewall", "Failed to create firewall policy instance", ex);
            return null;
        }
    }
}
