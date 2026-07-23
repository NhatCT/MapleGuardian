using System.Diagnostics;
using MapleGuardian.Models;
using File = System.IO.File;

namespace MapleGuardian.Services;

/// <summary>
/// SoftEther VPN reconnection service.
/// Uses vpncmd CLI to disconnect and reconnect the VPN account.
/// Implements retry logic with configurable delays.
/// </summary>
public class SoftEtherService
{
    private readonly AppConfig _config;
    private readonly LogService _log;
    private bool _isReconnecting;
    private CancellationTokenSource? _reconnectCts;

    public bool IsReconnecting => _isReconnecting;

    public event EventHandler? ReconnectStarted;
    public event EventHandler<bool>? ReconnectCompleted; // true = success

    public SoftEtherService(AppConfig config, LogService log)
    {
        _config = config;
        _log = log;
    }

    /// <summary>
    /// Attempt to reconnect VPN with retry logic
    /// </summary>
    public async Task<bool> ReconnectAsync()
    {
        if (_isReconnecting)
        {
            _log.Warning("SoftEther", "Reconnect already in progress");
            return false;
        }

        _isReconnecting = true;
        _reconnectCts = new CancellationTokenSource();
        ReconnectStarted?.Invoke(this, EventArgs.Empty);

        try
        {
            string accountName = await ResolveAccountNameAsync();
            _log.Info("SoftEther", $"Starting reconnection for account '{accountName}' (max {_config.MaxReconnectAttempts} attempts)");

            for (int attempt = 1; attempt <= _config.MaxReconnectAttempts; attempt++)
            {
                if (_reconnectCts.Token.IsCancellationRequested)
                {
                    _log.Info("SoftEther", "Reconnection cancelled");
                    return false;
                }

                _log.Info("SoftEther", $"Reconnect attempt {attempt}/{_config.MaxReconnectAttempts}...");

                // Disconnect first
                await ExecuteVpnCmd($"AccountDisconnect {accountName}");
                await Task.Delay(1000, _reconnectCts.Token);

                // Connect
                var connectResult = await ExecuteVpnCmd($"AccountConnect {accountName}");

                if (connectResult.Success)
                {
                    // Wait a bit then verify
                    await Task.Delay(3000, _reconnectCts.Token);
                    var status = await GetAccountStatus(accountName);

                    if (status.Contains("Connected", StringComparison.OrdinalIgnoreCase))
                    {
                        _log.Info("SoftEther", $"✅ VPN reconnected successfully on attempt {attempt}");
                        ReconnectCompleted?.Invoke(this, true);
                        return true;
                    }
                }

                _log.Warning("SoftEther", $"Attempt {attempt} failed, waiting {_config.ReconnectDelaySeconds}s...");

                if (attempt < _config.MaxReconnectAttempts)
                {
                    await Task.Delay(
                        TimeSpan.FromSeconds(_config.ReconnectDelaySeconds),
                        _reconnectCts.Token);
                }
            }

            _log.Error("SoftEther", $"❌ Reconnection failed after {_config.MaxReconnectAttempts} attempts");
            ReconnectCompleted?.Invoke(this, false);
            return false;
        }
        catch (OperationCanceledException)
        {
            _log.Info("SoftEther", "Reconnection cancelled");
            return false;
        }
        catch (Exception ex)
        {
            _log.Error("SoftEther", "Reconnection error", ex);
            ReconnectCompleted?.Invoke(this, false);
            return false;
        }
        finally
        {
            _isReconnecting = false;
        }
    }

    /// <summary>
    /// Cancel an ongoing reconnection attempt
    /// </summary>
    public void CancelReconnect()
    {
        _reconnectCts?.Cancel();
        _log.Info("SoftEther", "Reconnection cancel requested");
    }

    /// <summary>
    /// Resolve account name — auto-detect from SoftEther if configured name doesn't match
    /// </summary>
    public async Task<string> ResolveAccountNameAsync()
    {
        string configured = _config.SoftEtherAccountName;
        var listResult = await ExecuteVpnCmd("AccountList");
        if (!listResult.Success || string.IsNullOrWhiteSpace(listResult.Output))
            return configured;

        var lines = listResult.Output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        var accounts = new List<string>();
        foreach (var line in lines)
        {
            if (line.Contains("VPN Connection Setting Name") || line.Contains("Setting Name"))
            {
                var parts = line.Split('|');
                if (parts.Length >= 2)
                {
                    accounts.Add(parts[1].Trim());
                }
            }
        }

        if (accounts.Count > 0)
        {
            if (accounts.Exists(a => string.Equals(a, configured, StringComparison.OrdinalIgnoreCase)))
                return configured;

            _log.Info("SoftEther", $"Configured account '{configured}' not found. Auto-detected account: '{accounts[0]}'");
            return accounts[0];
        }

        return configured;
    }

    /// <summary>
    /// Get current VPN account status via vpncmd
    /// </summary>
    public async Task<string> GetAccountStatus(string? accountName = null)
    {
        accountName ??= await ResolveAccountNameAsync();
        var result = await ExecuteVpnCmd($"AccountStatusGet {accountName}");
        return result.Output;
    }

    private async Task<(bool Success, string Output)> ExecuteVpnCmd(string command)
    {
        try
        {
            var vpnCmdPath = ResolveVpnCmdPath();
            if (string.IsNullOrEmpty(vpnCmdPath))
            {
                _log.Error("SoftEther", $"vpncmd.exe not found! Checked configured path: {_config.SoftEtherPath} and standard locations.");
                return (false, "vpncmd not found");
            }

            var psi = new ProcessStartInfo
            {
                FileName = vpnCmdPath,
                Arguments = $"localhost /CLIENT /CMD {command}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process == null)
            {
                return (false, "Failed to start vpncmd");
            }

            var output = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            bool success = process.ExitCode == 0;
            if (!success && !string.IsNullOrEmpty(error))
            {
                _log.Warning("SoftEther", $"vpncmd error: {error.Trim()}");
            }

            return (success, output);
        }
        catch (Exception ex)
        {
            _log.Error("SoftEther", $"Failed to execute vpncmd: {command}", ex);
            return (false, ex.Message);
        }
    }

    private string? ResolveVpnCmdPath()
    {
        if (File.Exists(_config.SoftEtherPath))
            return _config.SoftEtherPath;

        string[] searchPaths = [
            @"C:\Program Files\SoftEther VPN Client\vpncmd.exe",
            @"C:\Program Files (x86)\SoftEther VPN Client\vpncmd.exe",
            @"C:\SoftEther VPN Client\vpncmd.exe"
        ];

        foreach (var path in searchPaths)
        {
            if (File.Exists(path))
            {
                _log.Info("SoftEther", $"Auto-detected vpncmd at: {path}");
                return path;
            }
        }

        return null;
    }
}
