using System.Windows;
using Hardcodet.Wpf.TaskbarNotification;

namespace MapleGuardian.Services;

/// <summary>
/// Windows notification service using system tray balloon tips.
/// Simple, reliable approach that works on all Windows versions.
/// </summary>
public class NotificationService
{
    private readonly LogService _log;
    private TaskbarIcon? _trayIcon;

    public NotificationService(LogService log)
    {
        _log = log;
    }

    /// <summary>
    /// Set the TaskbarIcon reference for balloon tips
    /// </summary>
    public void SetTaskbarIcon(TaskbarIcon icon)
    {
        _trayIcon = icon;
    }

    /// <summary>
    /// Show VPN disconnected warning
    /// </summary>
    public void NotifyVpnLost()
    {
        ShowBalloon("⚠️ VPN Disconnected!",
            "Firewall rules activated — game traffic BLOCKED.\nAttempting to reconnect...",
            BalloonIcon.Warning);
        _log.Info("Notification", "VPN lost notification sent");
    }

    /// <summary>
    /// Show VPN reconnected success
    /// </summary>
    public void NotifyVpnReconnected()
    {
        ShowBalloon("✅ VPN Reconnected!",
            "Connection restored — game traffic unblocked.",
            BalloonIcon.Info);
        _log.Info("Notification", "VPN reconnected notification sent");
    }

    /// <summary>
    /// Show reconnection failed critical alert
    /// </summary>
    public void NotifyReconnectFailed()
    {
        ShowBalloon("❌ VPN Reconnection Failed!",
            "Max retry attempts reached — game remains blocked.\nCheck VPN connection manually.",
            BalloonIcon.Error);
        _log.Warning("Notification", "Reconnect failed notification sent");
    }

    /// <summary>
    /// Show a generic info notification
    /// </summary>
    public void NotifyInfo(string title, string message)
    {
        ShowBalloon(title, message, BalloonIcon.Info);
    }

    /// <summary>
    /// Show balloon notification via system tray
    /// </summary>
    private void ShowBalloon(string title, string text, BalloonIcon icon)
    {
        try
        {
            if (_trayIcon != null)
            {
                System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                {
                    _trayIcon.ShowBalloonTip(title, text, icon);
                });
            }
        }
        catch (Exception ex)
        {
            _log.Warning("Notification", $"Failed to send notification: {ex.Message}");
        }
    }

    /// <summary>
    /// Clean up
    /// </summary>
    public static void ClearAll()
    {
        // No cleanup needed for balloon tips
    }
}
