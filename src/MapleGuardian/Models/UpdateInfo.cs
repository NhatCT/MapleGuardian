namespace MapleGuardian.Models;

/// <summary>
/// Remote version info returned by update server / GitHub releases
/// </summary>
public class UpdateInfo
{
    public string Version { get; set; } = "2.0.0";
    public string DownloadUrl { get; set; } = string.Empty;
    public string Changelog { get; set; } = string.Empty;
    public bool Mandatory { get; set; }
}
