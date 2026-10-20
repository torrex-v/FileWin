using System;
using System.IO;

namespace FileWin.Models;

public class DriveItem
{
    public string Name { get; set; } = string.Empty;
    public string RootPath { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string DriveType { get; set; } = string.Empty;
    public long TotalSize { get; set; }
    public long FreeSpace { get; set; }
    public double PercentUsed { get; set; }
    public string DisplayText { get; set; } = string.Empty;
    public string Icon { get; set; } = "💾";

    public static DriveItem FromDriveInfo(DriveInfo d)
    {
        string label = string.Empty;
        long total = 0;
        long free = 0;
        double pct = 0;

        try
        {
            if (d.IsReady)
            {
                label = string.IsNullOrWhiteSpace(d.VolumeLabel) ? d.DriveType.ToString() : d.VolumeLabel;
                total = d.TotalSize;
                free = d.AvailableFreeSpace;
                if (total > 0)
                {
                    pct = (double)(total - free) / total * 100.0;
                }
            }
        }
        catch { }

        string root = d.RootDirectory.FullName;
        string freeStr = FileItem.FormatBytes(free);
        string totalStr = FileItem.FormatBytes(total);

        return new DriveItem
        {
            Name = d.Name.TrimEnd('\\'),
            RootPath = root,
            Label = label,
            DriveType = d.DriveType.ToString(),
            TotalSize = total,
            FreeSpace = free,
            PercentUsed = pct,
            DisplayText = $"{d.Name.TrimEnd('\\')} {label} ({freeStr} free of {totalStr})",
            Icon = d.DriveType == System.IO.DriveType.Network ? "🌐" : "💾"
        };
    }
}

public class QuickAccessItem
{
    public string Title { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string Group { get; set; } = string.Empty; // Storage, Places, Bookmarks, Recents
    public string Icon { get; set; } = "📁";
    public bool HasFarsi { get; set; }
    public bool IsRemovable { get; set; }
}
