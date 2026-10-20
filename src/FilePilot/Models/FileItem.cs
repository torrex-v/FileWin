using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using FilePilot.Services;

namespace FilePilot.Models;

public class FileItem : INotifyPropertyChanged
{
    private bool _isExpanded;
    private bool _isSelected;
    private bool _isCut;
    private long _size;
    private int _itemCount;
    private bool _isCalculating;
    private string _formattedSize = string.Empty;

    public string Name { get; set; } = string.Empty;
    public string FullPath { get; set; } = string.Empty;
    public string Extension { get; set; } = string.Empty;
    public bool IsDirectory { get; set; }
    public bool IsHidden { get; set; }
    public bool IsSystem { get; set; }
    public DateTime DateModified { get; set; }
    public DateTime DateCreated { get; set; }
    public DateTime DateAccessed { get; set; }
    public string AttributesString { get; set; } = string.Empty;

    // Farsi Recognition Features
    public bool HasFarsiCharacters { get; set; }
    public FarsiCharacterStats? FarsiStats { get; set; }
    public string FarsiBadgeText => HasFarsiCharacters ? "فا" : string.Empty;
    public string FarsiTooltip => HasFarsiCharacters 
        ? $"Farsi/Persian text recognized ({FarsiStats?.FarsiCharCount} chars, {FarsiStats?.PersianSpecificCharCount} Persian letters)" 
        : string.Empty;

    // Tree hierarchy
    public int IndentLevel { get; set; } = 0;
    public ObservableCollection<FileItem> Children { get; set; } = new();

    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (_isExpanded != value)
            {
                _isExpanded = value;
                OnPropertyChanged();
            }
        }
    }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected != value)
            {
                _isSelected = value;
                OnPropertyChanged();
            }
        }
    }

    public bool IsCut
    {
        get => _isCut;
        set
        {
            if (_isCut != value)
            {
                _isCut = value;
                OnPropertyChanged();
            }
        }
    }

    public long Size
    {
        get => _size;
        set
        {
            _size = value;
            FormattedSize = FormatBytes(value);
            OnPropertyChanged();
        }
    }

    public string FormattedSize
    {
        get => _formattedSize;
        set
        {
            _formattedSize = value;
            OnPropertyChanged();
        }
    }

    public int ItemCount
    {
        get => _itemCount;
        set
        {
            _itemCount = value;
            OnPropertyChanged();
        }
    }

    public bool IsCalculating
    {
        get => _isCalculating;
        set
        {
            _isCalculating = value;
            OnPropertyChanged();
        }
    }

    public string IconKind => IsDirectory 
        ? (IsExpanded ? "📂" : "📁") 
        : GetIconForExtension(Extension);

    public static string GetIconForExtension(string ext)
    {
        return ext.ToLowerInvariant() switch
        {
            ".exe" or ".bat" or ".cmd" or ".ps1" or ".msi" => "⚙️",
            ".zip" or ".rar" or ".7z" or ".tar" or ".gz" => "📦",
            ".txt" or ".md" or ".log" or ".rtf" => "📝",
            ".cs" or ".cpp" or ".h" or ".py" or ".js" or ".ts" or ".html" or ".css" or ".json" or ".xml" => "💻",
            ".png" or ".jpg" or ".jpeg" or ".gif" or ".bmp" or ".svg" or ".webp" or ".ico" => "🖼️",
            ".mp3" or ".wav" or ".flac" or ".ogg" or ".m4a" => "🎵",
            ".mp4" or ".mkv" or ".avi" or ".mov" or ".webm" => "🎬",
            ".pdf" => "📕",
            ".docx" or ".doc" => "📘",
            ".xlsx" or ".xls" or ".csv" => "📊",
            ".pptx" or ".ppt" => "📙",
            ".dll" or ".sys" => "🧩",
            _ => "📄"
        };
    }

    public static string FormatBytes(long bytes)
    {
        if (bytes < 0) return "--";
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{(bytes / 1024.0):F1} KB";
        if (bytes < 1024 * 1024 * 1024) return $"{(bytes / (1024.0 * 1024.0)):F1} MB";
        return $"{(bytes / (1024.0 * 1024.0 * 1024.0)):F2} GB";
    }

    public static FileItem FromFileSystemInfo(FileSystemInfo info, int indent = 0)
    {
        bool isDir = (info.Attributes & FileAttributes.Directory) != 0;
        long size = 0;
        string ext = isDir ? "<DIR>" : info.Extension;

        if (!isDir && info is FileInfo fi)
        {
            try { size = fi.Length; } catch { size = 0; }
        }

        var farsiStats = FarsiRecognitionEngine.Analyze(info.Name);

        var item = new FileItem
        {
            Name = info.Name,
            FullPath = info.FullName,
            Extension = ext,
            IsDirectory = isDir,
            IsHidden = (info.Attributes & FileAttributes.Hidden) != 0,
            IsSystem = (info.Attributes & FileAttributes.System) != 0,
            DateModified = info.LastWriteTime,
            DateCreated = info.CreationTime,
            DateAccessed = info.LastAccessTime,
            AttributesString = info.Attributes.ToString(),
            IndentLevel = indent,
            Size = size,
            HasFarsiCharacters = farsiStats.HasFarsi,
            FarsiStats = farsiStats,
            FormattedSize = isDir ? "<DIR>" : FormatBytes(size)
        };

        return item;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
