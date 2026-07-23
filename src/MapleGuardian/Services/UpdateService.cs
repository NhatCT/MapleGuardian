using System.Diagnostics;
using System.Net.Http;
using System.Text.Json;
using MapleGuardian.Models;
using File = System.IO.File;
using Path = System.IO.Path;
using Directory = System.IO.Directory;

namespace MapleGuardian.Services;

/// <summary>
/// Auto-Updater Service:
/// Checks remote update server/GitHub for version updates.
/// Automatically downloads and updates executable files seamlessly.
/// </summary>
public class UpdateService
{
    private readonly AppConfig _config;
    private readonly LogService _log;
    private readonly HttpClient _httpClient;

    public event EventHandler<UpdateInfo>? UpdateAvailable;

    public UpdateService(AppConfig config, LogService log)
    {
        _config = config;
        _log = log;
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
    }

    /// <summary>
    /// Check for online updates asynchronously
    /// </summary>
    public async Task<UpdateInfo?> CheckForUpdatesAsync()
    {
        if (!_config.EnableAutoUpdate || string.IsNullOrEmpty(_config.UpdateUrl))
            return null;

        try
        {
            _log.Info("Update", $"Checking for updates from: {_config.UpdateUrl}");
            var jsonStr = await _httpClient.GetStringAsync(_config.UpdateUrl);
            var updateInfo = JsonSerializer.Deserialize<UpdateInfo>(jsonStr, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (updateInfo != null)
            {
                var currentVer = GetCurrentVersion();
                if (Version.TryParse(updateInfo.Version, out var remoteVer) && remoteVer > currentVer)
                {
                    _log.Info("Update", $"🎉 New version available: {remoteVer} (Current: {currentVer})");
                    UpdateAvailable?.Invoke(this, updateInfo);
                    return updateInfo;
                }
                else
                {
                    _log.Info("Update", $"Application is up to date (Current: {currentVer})");
                }
            }
        }
        catch (Exception ex)
        {
            _log.Warning("Update", $"Update check skipped/failed: {ex.Message}");
        }

        return null;
    }

    /// <summary>
    /// Download update and trigger auto-replacement script
    /// </summary>
    public async Task<bool> ApplyUpdateAsync(UpdateInfo info)
    {
        try
        {
            if (string.IsNullOrEmpty(info.DownloadUrl))
                return false;

            _log.Info("Update", $"Downloading update from: {info.DownloadUrl}");
            var tempDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "temp_update");
            Directory.CreateDirectory(tempDir);

            var downloadPath = Path.Combine(tempDir, "update.zip");
            var bytes = await _httpClient.GetByteArrayAsync(info.DownloadUrl);
            await File.WriteAllBytesAsync(downloadPath, bytes);

            _log.Info("Update", "Update downloaded. Creating update script...");
            CreateAndLaunchUpdater(downloadPath, tempDir);

            return true;
        }
        catch (Exception ex)
        {
            _log.Error("Update", "Failed to apply update", ex);
            return false;
        }
    }

    /// <summary>
    /// Launch background batch script that overwrites app executable after exit and restarts app
    /// </summary>
    private static void CreateAndLaunchUpdater(string zipPath, string tempDir)
    {
        var currentExe = Environment.ProcessPath ?? "MapleGuardian.exe";
        var batchPath = Path.Combine(tempDir, "update.bat");

        var script = $@"@echo off
timeout /t 2 /nobreak >nul
powershell -Command ""Expand-Archive -Path '{zipPath}' -DestinationPath '{AppDomain.CurrentDomain.BaseDirectory}' -Force""
start ""MapleGuardian"" ""{currentExe}""
rmdir /s /q ""{tempDir}""
";

        File.WriteAllText(batchPath, script);

        var psi = new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = $"/c \"{batchPath}\"",
            UseShellExecute = true,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden
        };

        Process.Start(psi);
        System.Windows.Application.Current?.Shutdown();
    }

    public static Version GetCurrentVersion()
    {
        var asm = System.Reflection.Assembly.GetExecutingAssembly();
        return asm.GetName().Version ?? new Version(2, 0, 0);
    }
}
