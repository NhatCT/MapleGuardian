using Microsoft.Win32;

namespace MapleGuardian.Services;

/// <summary>
/// Manages Windows startup registration via the Registry.
/// Adds/removes the app from HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Run.
/// </summary>
public class StartupManager
{
    private const string RegistryKeyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "MapleGuardian";
    private readonly LogService _log;

    public StartupManager(LogService log)
    {
        _log = log;
    }

    /// <summary>
    /// Check if app is registered to start with Windows
    /// </summary>
    public bool IsRegistered()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath, false);
            return key?.GetValue(AppName) != null;
        }
        catch (Exception ex)
        {
            _log.Error("Startup", "Failed to check startup registration", ex);
            return false;
        }
    }

    /// <summary>
    /// Register app to start with Windows
    /// </summary>
    public bool Register()
    {
        try
        {
            var exePath = Environment.ProcessPath;
            if (string.IsNullOrEmpty(exePath))
            {
                _log.Error("Startup", "Could not determine executable path");
                return false;
            }

            using var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath, true);
            if (key == null)
            {
                _log.Error("Startup", "Could not open registry key");
                return false;
            }

            key.SetValue(AppName, $"\"{exePath}\" --minimized");
            _log.Info("Startup", $"Registered for startup: {exePath}");
            return true;
        }
        catch (Exception ex)
        {
            _log.Error("Startup", "Failed to register startup", ex);
            return false;
        }
    }

    /// <summary>
    /// Unregister app from Windows startup
    /// </summary>
    public bool Unregister()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath, true);
            if (key?.GetValue(AppName) != null)
            {
                key.DeleteValue(AppName);
                _log.Info("Startup", "Removed from startup");
            }
            return true;
        }
        catch (Exception ex)
        {
            _log.Error("Startup", "Failed to unregister startup", ex);
            return false;
        }
    }

    /// <summary>
    /// Toggle startup registration
    /// </summary>
    public bool Toggle()
    {
        return IsRegistered() ? Unregister() : Register();
    }
}
