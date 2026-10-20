using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace FilePilot.Models;

public class SessionFileModel
{
    [JsonPropertyName("Version")]
    public int Version { get; set; } = 1;

    [JsonPropertyName("Layout")]
    public SessionLayoutModel Layout { get; set; } = new();
}

public class SessionLayoutModel
{
    public int WindowX { get; set; } = 100;
    public int WindowY { get; set; } = 100;
    public int WindowWidth { get; set; } = 1200;
    public int WindowHeight { get; set; } = 800;
    public bool WindowMaximized { get; set; } = false;
    public double QuickAccessWidth { get; set; } = 220;
    public bool QuickAccessClosed { get; set; } = false;
    public bool QuickAccessShowPaths { get; set; } = false;

    public List<SessionGroupModel> QuickAccessGroups { get; set; } = new()
    {
        new() { Name = "Recents", IsClosed = false, SortIndex = 0 },
        new() { Name = "Bookmarks", IsClosed = false, SortIndex = 1 },
        new() { Name = "Storage", IsClosed = false, SortIndex = 2 },
        new() { Name = "Places", IsClosed = false, SortIndex = 3 }
    };

    public List<SessionPanelModel> Panels { get; set; } = new();
    public int SelectedPanel { get; set; } = 1;

    // Farsi Recognition Feature Persistence
    public bool FilterFarsiOnly { get; set; } = false;
    public bool InspectorVisible { get; set; } = true;
    public double InspectorWidth { get; set; } = 300;
    public bool SplitDualView { get; set; } = false;
    public string SplitOrientation { get; set; } = "Horizontal"; // Horizontal or Vertical
    public double SplitRatio { get; set; } = 0.5;
}

public class SessionGroupModel
{
    public string Name { get; set; } = string.Empty;
    public bool IsClosed { get; set; }
    public int SortIndex { get; set; }
}

public class SessionPanelModel
{
    public int ID { get; set; } = 1;
    public double SplitRatio { get; set; } = 0.5;
    public string Path { get; set; } = "C:/";
    public bool Expanded { get; set; } = false;
    public bool Hidden { get; set; } = false;
    public bool FilterSticky { get; set; } = false;
    public bool HideFiles { get; set; } = false;
    public bool HideDirs { get; set; } = false;
    public double ZoomLevel { get; set; } = 0.0;
    public int SortedBy { get; set; } = 0; // 0=Name, 1=Size, 2=Type, 3=DateModified
    public bool SortAscending { get; set; } = true;
    public string FolderSort { get; set; } = "FoldersFirst"; // FoldersFirst, FoldersLast, Mixed
    public List<SessionColumnModel> Columns { get; set; } = new();
}

public class SessionColumnModel
{
    public int Kind { get; set; }
    public double SplitRatio { get; set; }
}
