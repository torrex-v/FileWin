using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;

namespace FileWin.Models;

public class PanelTab : INotifyPropertyChanged
{
    private string _currentPath = string.Empty;
    private string _title = string.Empty;
    private bool _isSelected;

    public string Id { get; set; } = Guid.NewGuid().ToString();

    public string CurrentPath
    {
        get => _currentPath;
        set
        {
            if (_currentPath != value)
            {
                _currentPath = value;
                UpdateTitle();
                OnPropertyChanged();
            }
        }
    }

    public string Title
    {
        get => _title;
        set
        {
            if (_title != value)
            {
                _title = value;
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

    public List<string> HistoryBack { get; } = new();
    public List<string> HistoryForward { get; } = new();

    public void NavigateTo(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        if (!string.IsNullOrEmpty(CurrentPath) && !string.Equals(CurrentPath, path, StringComparison.OrdinalIgnoreCase))
        {
            HistoryBack.Add(CurrentPath);
            HistoryForward.Clear();
        }
        CurrentPath = path;
    }

    public bool CanGoBack => HistoryBack.Count > 0;
    public bool CanGoForward => HistoryForward.Count > 0;

    public string? GoBack()
    {
        if (HistoryBack.Count == 0) return null;
        int lastIdx = HistoryBack.Count - 1;
        string target = HistoryBack[lastIdx];
        HistoryBack.RemoveAt(lastIdx);
        if (!string.IsNullOrEmpty(CurrentPath))
        {
            HistoryForward.Add(CurrentPath);
        }
        _currentPath = target;
        UpdateTitle();
        OnPropertyChanged(nameof(CurrentPath));
        return target;
    }

    public string? GoForward()
    {
        if (HistoryForward.Count == 0) return null;
        int lastIdx = HistoryForward.Count - 1;
        string target = HistoryForward[lastIdx];
        HistoryForward.RemoveAt(lastIdx);
        if (!string.IsNullOrEmpty(CurrentPath))
        {
            HistoryBack.Add(CurrentPath);
        }
        _currentPath = target;
        UpdateTitle();
        OnPropertyChanged(nameof(CurrentPath));
        return target;
    }

    private void UpdateTitle()
    {
        if (string.IsNullOrEmpty(_currentPath))
        {
            Title = "New Tab";
            return;
        }

        string trimmed = _currentPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (trimmed.Length <= 3 && trimmed.EndsWith(':'))
        {
            Title = trimmed;
        }
        else
        {
            Title = Path.GetFileName(trimmed);
            if (string.IsNullOrEmpty(Title))
                Title = trimmed;
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public class CommandItem
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = "General";
    public string Hotkey { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public Action? Action { get; set; }
}
