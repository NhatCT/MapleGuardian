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
            _log.Info("SoftEther", $"Starting reconnection (max {_config.MaxReconnectAttempts} attempts)");

            for (int attempt = 1; attempt <= _config.MaxReconnectAttempts; attempt++)
            {
                if (_reconnectCts.Token.IsCancellationRequested)
                {
                    _log.Info("SoftEther", "Reconnection cancelled");
                    return false;
                }

                _log.Info("SoftEther", $"Reconnect attempt {attempt}/{_config.MaxReconnectAttempts}...");

                // Disconnect first
                await ExecuteVpnCmd($"AccountDisconnect {_config.SoftEtherAccountName}");
                await Task.Delay(1000, _reconnectCts.Token);

                // Connect
                var connectResult = await ExecuteVpnCmd($"AccountConnect {_config.SoftEtherAccountName}");

                if (connectResult.Success)
                {
                    // Wait a bit then verify
                    await Task.Delay(3000, _reconnectCts.Token);
                    var status = await GetAccountStatus();

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
    /// Get current VPN account status via vpncmd
    /// </summary>
    public async Task<string> GetAccountStatus()
    {
        var result = await ExecuteVpnCmd($"AccountStatusGet {_config.SoftEtherAccountName}");
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
