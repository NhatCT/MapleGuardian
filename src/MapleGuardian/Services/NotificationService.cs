using System.Windows;

namespace MapleGuardian.Services;

/// <summary>
/// Windows notification service using system tray balloon tips.
/// Simple, reliable approach that works on all Windows versions.
/// </summary>
public class NotificationService
{
    private readonly LogService _log;
    private System.Windows.Forms.NotifyIcon? _notifyIcon;

    public NotificationService(LogService log)
    {
        _log = log;
    }

    /// <summary>
    /// Set the NotifyIcon reference for balloon tips
    /// </summary>
    public void SetNotifyIcon(System.Windows.Forms.NotifyIcon icon)
    {
        _notifyIcon = icon;
    }

    /// <summary>
    /// Show VPN disconnected warning
    /// </summary>
    public void NotifyVpnLost()
    {
        ShowBalloon("⚠️ VPN Disconnected!",
            "Firewall rules activated — game traffic BLOCKED.\nAttempting to reconnect...",
            System.Windows.Forms.ToolTipIcon.Warning);
        _log.Info("Notification", "VPN lost notification sent");
    }

    /// <summary>
    /// Show VPN reconnected success
    /// </summary>
    public void NotifyVpnReconnected()
    {
        ShowBalloon("✅ VPN Reconnected!",
            "Connection restored — game traffic unblocked.",
            System.Windows.Forms.ToolTipIcon.Info);
        _log.Info("Notification", "VPN reconnected notification sent");
    }

    /// <summary>
    /// Show reconnection failed critical alert
    /// </summary>
    public void NotifyReconnectFailed()
    {
        ShowBalloon("❌ VPN Reconnection Failed!",
            "Max retry attempts reached — game remains blocked.\nCheck VPN connection manually.",
            System.Windows.Forms.ToolTipIcon.Error);
        _log.Warning("Notification", "Reconnect failed notification sent");
    }

    /// <summary>
    /// Show a generic info notification
    /// </summary>
    public void NotifyInfo(string title, string message)
    {
        ShowBalloon(title, message, System.Windows.Forms.ToolTipIcon.Info);
    }

    /// <summary>
    /// Show balloon notification via system tray
    /// </summary>
    private void ShowBalloon(string title, string text, System.Windows.Forms.ToolTipIcon icon)
    {
        try
        {
            if (_notifyIcon != null)
            {
                _notifyIcon.BalloonTipTitle = title;
                _notifyIcon.BalloonTipText = text;
                _notifyIcon.BalloonTipIcon = icon;
                _notifyIcon.ShowBalloonTip(5000);
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
