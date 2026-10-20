using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using FileWin.Models;
using FileWin.Services;

namespace FileWin.ViewModels;

public class MainViewModel : INotifyPropertyChanged
{
    private PanelTab? _activeTab;
    private FileItem? _selectedItem;
    private string _filterText = string.Empty;
    private bool _filterFarsiOnly = false;
    private bool _searchSubfolders = false;
    private bool _isDualPanel = false;
    private string _splitOrientation = "Horizontal";
    private bool _isInspectorVisible = true;
    private string _inspectorContent = string.Empty;
    private BitmapImage? _inspectorImage;
    private string _inspectorSha256 = string.Empty;
    private string _inspectorFarsiSummary = string.Empty;
    private bool _inspectorIsRtl = false;
    private string _statusText = string.Empty;
    private string _currentSortColumn = "Name";
    private bool _isSortAscending = true;
    private bool _isLoading = false;

    private readonly DirectoryWatcherService _watcher;
    private CancellationTokenSource? _folderSizeCts;
    private CancellationTokenSource? _navigateCts;
    private List<FileItem> _allItems = new();
    private List<string> _clipboardPaths = new();
    private bool _isClipboardCut = false;

    public ObservableCollection<PanelTab> Tabs { get; } = new();
    public ObservableCollection<FileItem> DisplayItems { get; } = new();
    public ObservableCollection<DriveItem> Drives { get; } = new();
    public ObservableCollection<QuickAccessItem> Places { get; } = new();
    public ObservableCollection<QuickAccessItem> Bookmarks { get; } = new();
    public ObservableCollection<QuickAccessItem> Recents { get; } = new();
    public ObservableCollection<CommandItem> AllCommands { get; } = new();

    public MainViewModel()
    {
        _watcher = new DirectoryWatcherService(() =>
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                _ = RefreshCurrentDirectoryAsync(preserveSelection: true);
            });
        });

        InitializeDrivesAndPlaces();
        InitializeCommands();

        var session = SessionManager.LoadSession();
        _isDualPanel = session.Layout.SplitDualView;
        _splitOrientation = session.Layout.SplitOrientation ?? "Horizontal";
        _isInspectorVisible = session.Layout.InspectorVisible;
        _filterFarsiOnly = session.Layout.FilterFarsiOnly;

        string initialPath = "C:\\";
        if (session.Layout.Panels.Count > 0 && Directory.Exists(session.Layout.Panels[0].Path))
        {
            initialPath = session.Layout.Panels[0].Path;
        }
        else
        {
            string userHome = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (Directory.Exists(userHome)) initialPath = userHome;
        }

        var tab = new PanelTab { CurrentPath = initialPath };
        Tabs.Add(tab);
        ActiveTab = tab;
    }

    public bool IsLoading
    {
        get => _isLoading;
        set { _isLoading = value; OnPropertyChanged(); }
    }

    public PanelTab? ActiveTab
    {
        get => _activeTab;
        set
        {
            if (_activeTab != value)
            {
                if (_activeTab != null) _activeTab.IsSelected = false;
                _activeTab = value;
                if (_activeTab != null)
                {
                    _activeTab.IsSelected = true;
                    NavigateToAsync(_activeTab.CurrentPath, addToHistory: false);
                }
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanGoBack));
                OnPropertyChanged(nameof(CanGoForward));
            }
        }
    }

    public FileItem? SelectedItem
    {
        get => _selectedItem;
        set
        {
            if (_selectedItem != value)
            {
                _selectedItem = value;
                OnPropertyChanged();
                UpdateInspectorAsync();
                UpdateStatus();
            }
        }
    }

    public string FilterText
    {
        get => _filterText;
        set
        {
            if (_filterText != value)
            {
                _filterText = value;
                OnPropertyChanged();
                ApplyFilterAndSort();
            }
        }
    }

    public bool FilterFarsiOnly
    {
        get => _filterFarsiOnly;
        set
        {
            if (_filterFarsiOnly != value)
            {
                _filterFarsiOnly = value;
                OnPropertyChanged();
                ApplyFilterAndSort();
                UpdateStatus();
            }
        }
    }

    public bool SearchSubfolders
    {
        get => _searchSubfolders;
        set
        {
            if (_searchSubfolders != value)
            {
                _searchSubfolders = value;
                OnPropertyChanged();
                if (ActiveTab != null)
                {
                    if (_searchSubfolders) RunSubfolderSearchAsync();
                    else ApplyFilterAndSort();
                }
            }
        }
    }

    public bool IsDualPanel
    {
        get => _isDualPanel;
        set { _isDualPanel = value; OnPropertyChanged(); }
    }

    public string SplitOrientation
    {
        get => _splitOrientation;
        set { _splitOrientation = value; OnPropertyChanged(); }
    }

    public bool IsInspectorVisible
    {
        get => _isInspectorVisible;
        set
        {
            if (_isInspectorVisible != value)
            {
                _isInspectorVisible = value;
                OnPropertyChanged();
                if (_isInspectorVisible) UpdateInspectorAsync();
            }
        }
    }

    public string InspectorContent
    {
        get => _inspectorContent;
        set { _inspectorContent = value; OnPropertyChanged(); }
    }

    public BitmapImage? InspectorImage
    {
        get => _inspectorImage;
        set { _inspectorImage = value; OnPropertyChanged(); }
    }

    public string InspectorSha256
    {
        get => _inspectorSha256;
        set { _inspectorSha256 = value; OnPropertyChanged(); }
    }

    public string InspectorFarsiSummary
    {
        get => _inspectorFarsiSummary;
        set { _inspectorFarsiSummary = value; OnPropertyChanged(); }
    }

    public bool InspectorIsRtl
    {
        get => _inspectorIsRtl;
        set { _inspectorIsRtl = value; OnPropertyChanged(); }
    }

    public string StatusText
    {
        get => _statusText;
        set { _statusText = value; OnPropertyChanged(); }
    }

    public bool CanGoBack => ActiveTab?.CanGoBack ?? false;
    public bool CanGoForward => ActiveTab?.CanGoForward ?? false;

    // Fully Asynchronous Navigation
    public async void NavigateToAsync(string path, bool addToHistory = true)
    {
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
            return;

        if (ActiveTab != null)
        {
            if (addToHistory) ActiveTab.NavigateTo(path);
            else ActiveTab.CurrentPath = path;
        }

        AddRecent(path);
        _watcher.WatchDirectory(path);

        await RefreshCurrentDirectoryAsync();

        OnPropertyChanged(nameof(CanGoBack));
        OnPropertyChanged(nameof(CanGoForward));
    }

    public async Task RefreshCurrentDirectoryAsync(bool preserveSelection = false)
    {
        if (ActiveTab == null || !Directory.Exists(ActiveTab.CurrentPath))
            return;

        string currentPath = ActiveTab.CurrentPath;
        string? selectedPath = SelectedItem?.FullPath;

        _navigateCts?.Cancel();
        _navigateCts = new CancellationTokenSource();
        var token = _navigateCts.Token;

        IsLoading = true;
        StatusText = $"Loading {currentPath}...";

        try
        {
            var items = await Task.Run(() =>
            {
                return FileSystemService.GetDirectoryItems(currentPath);
            }, token);

            if (token.IsCancellationRequested) return;

            _allItems = items;
            ApplyFilterAndSort();

            if (preserveSelection && !string.IsNullOrEmpty(selectedPath))
            {
                SelectedItem = DisplayItems.FirstOrDefault(x => x.FullPath == selectedPath);
            }

            StartAsyncFolderSizeCalculation();
        }
        catch (OperationCanceledException) { }
        finally
        {
            IsLoading = false;
            UpdateStatus();
        }
    }

    public void ApplyFilterAndSort()
    {
        IEnumerable<FileItem> items = _allItems;

        // Farsi Only Filter
        if (FilterFarsiOnly)
        {
            items = items.Where(x => x.HasFarsiCharacters);
        }

        // Query Filter
        if (!string.IsNullOrWhiteSpace(FilterText))
        {
            string query = FilterText.Trim();
            items = items.Where(x =>
                FarsiRecognitionEngine.MatchesFarsi(query, x.Name) ||
                x.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                x.Extension.Contains(query, StringComparison.OrdinalIgnoreCase));
        }

        items = SortItems(items, _currentSortColumn, _isSortAscending);

        DisplayItems.Clear();
        foreach (var item in items)
        {
            DisplayItems.Add(item);
        }

        UpdateStatus();
    }

    // In-Line Folder Expansion (Tree-in-List: Ctrl+E)
    public async Task ToggleExpandFolderAsync(FileItem? item)
    {
        if (item == null || !item.IsDirectory) return;

        if (item.IsExpanded)
        {
            // Collapse: remove all descendants
            item.IsExpanded = false;
            int parentIdx = DisplayItems.IndexOf(item);
            if (parentIdx < 0) return;

            int removeIdx = parentIdx + 1;
            while (removeIdx < DisplayItems.Count && DisplayItems[removeIdx].IndentLevel > item.IndentLevel)
            {
                DisplayItems.RemoveAt(removeIdx);
            }
        }
        else
        {
            // Expand: load children asynchronously and insert
            item.IsExpanded = true;
            int parentIdx = DisplayItems.IndexOf(item);
            if (parentIdx < 0) return;

            int indent = item.IndentLevel + 1;
            var children = await Task.Run(() =>
            {
                var list = FileSystemService.GetDirectoryItems(item.FullPath);
                foreach (var c in list) c.IndentLevel = indent;
                return list;
            });

            var sorted = SortItems(children, _currentSortColumn, _isSortAscending);

            int insertIdx = parentIdx + 1;
            foreach (var child in sorted)
            {
                DisplayItems.Insert(insertIdx++, child);
            }
        }
    }

    public void SortByColumn(string columnName)
    {
        if (_currentSortColumn == columnName)
        {
            _isSortAscending = !_isSortAscending;
        }
        else
        {
            _currentSortColumn = columnName;
            _isSortAscending = true;
        }

        ApplyFilterAndSort();
    }

    private IEnumerable<FileItem> SortItems(IEnumerable<FileItem> items, string column, bool ascending)
    {
        IOrderedEnumerable<FileItem> ordered = items.OrderByDescending(x => x.IsDirectory);

        switch (column)
        {
            case "Name":
                ordered = ascending
                    ? ordered.ThenBy(x => x.Name, Comparer<string>.Create(FarsiRecognitionEngine.CompareFarsi))
                    : ordered.ThenByDescending(x => x.Name, Comparer<string>.Create(FarsiRecognitionEngine.CompareFarsi));
                break;
            case "Size":
                ordered = ascending ? ordered.ThenBy(x => x.Size) : ordered.ThenByDescending(x => x.Size);
                break;
            case "Type":
                ordered = ascending ? ordered.ThenBy(x => x.Extension) : ordered.ThenByDescending(x => x.Extension);
                break;
            case "DateModified":
                ordered = ascending ? ordered.ThenBy(x => x.DateModified) : ordered.ThenByDescending(x => x.DateModified);
                break;
            case "DateCreated":
                ordered = ascending ? ordered.ThenBy(x => x.DateCreated) : ordered.ThenByDescending(x => x.DateCreated);
                break;
            case "HasFarsiCharacters":
                ordered = ascending ? ordered.ThenBy(x => x.HasFarsiCharacters) : ordered.ThenByDescending(x => x.HasFarsiCharacters);
                break;
        }

        return ordered;
    }

    private async void RunSubfolderSearchAsync()
    {
        if (ActiveTab == null || string.IsNullOrWhiteSpace(FilterText)) return;
        string root = ActiveTab.CurrentPath;
        string query = FilterText.Trim();

        DisplayItems.Clear();
        IsLoading = true;
        StatusText = "Searching subfolders...";

        await Task.Run(() =>
        {
            try
            {
                var dir = new DirectoryInfo(root);
                var matches = dir.EnumerateFileSystemInfos("*", SearchOption.AllDirectories)
                    .Where(f => FarsiRecognitionEngine.MatchesFarsi(query, f.Name) ||
                                f.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
                    .Take(500)
                    .Select(f => FileItem.FromFileSystemInfo(f))
                    .ToList();

                Application.Current.Dispatcher.Invoke(() =>
                {
                    foreach (var m in matches) DisplayItems.Add(m);
                    IsLoading = false;
                    UpdateStatus();
                });
            }
            catch
            {
                Application.Current.Dispatcher.Invoke(() => IsLoading = false);
            }
        });
    }

    private void StartAsyncFolderSizeCalculation()
    {
        _folderSizeCts?.Cancel();
        _folderSizeCts = new CancellationTokenSource();
        var token = _folderSizeCts.Token;

        var directories = _allItems.Where(x => x.IsDirectory).ToList();

        Task.Run(async () =>
        {
            // Give initial UI render priority
            await Task.Delay(100, token);

            foreach (var dir in directories)
            {
                if (token.IsCancellationRequested) break;
                var (size, files, folders) = await FileSystemService.CalculateFolderSizeAsync(dir.FullPath, token);
                if (token.IsCancellationRequested) break;

                Application.Current.Dispatcher.Invoke(() =>
                {
                    dir.Size = size;
                    dir.ItemCount = files + folders;
                    dir.FormattedSize = FileItem.FormatBytes(size);
                });
            }
        }, token);
    }

    private async void UpdateInspectorAsync()
    {
        if (!_isInspectorVisible || SelectedItem == null)
        {
            InspectorContent = string.Empty;
            InspectorImage = null;
            InspectorSha256 = string.Empty;
            InspectorIsRtl = false;
            return;
        }

        var item = SelectedItem;
        string path = item.FullPath;

        if (!item.IsDirectory && File.Exists(path))
        {
            // SHA256 async calculation
            _ = Task.Run(() =>
            {
                string hash = FileSystemService.ComputeSha256(path);
                Application.Current.Dispatcher.Invoke(() =>
                {
                    if (SelectedItem?.FullPath == path)
                        InspectorSha256 = hash;
                });
            });

            string ext = item.Extension.ToLowerInvariant();
            if (ext is ".png" or ".jpg" or ".jpeg" or ".bmp" or ".gif" or ".ico" or ".webp")
            {
                try
                {
                    await Task.Run(() =>
                    {
                        var bitmap = new BitmapImage();
                        bitmap.BeginInit();
                        bitmap.UriSource = new Uri(path);
                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        bitmap.DecodePixelWidth = 400;
                        bitmap.EndInit();
                        bitmap.Freeze();

                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            if (SelectedItem?.FullPath == path)
                            {
                                InspectorImage = bitmap;
                                InspectorContent = string.Empty;
                            }
                        });
                    });
                }
                catch
                {
                    InspectorImage = null;
                }
            }
            else if (ext is ".txt" or ".md" or ".json" or ".xml" or ".cs" or ".cpp" or ".h" or ".py" or ".js" or ".log" or ".ini")
            {
                InspectorImage = null;
                await Task.Run(() =>
                {
                    try
                    {
                        var lines = File.ReadLines(path).Take(150).ToList();
                        string sampleText = string.Join(Environment.NewLine, lines);
                        var textFarsi = FarsiRecognitionEngine.Analyze(sampleText);

                        var numbered = lines.Select((line, i) => $"{(i + 1),4} | {line}");
                        string text = string.Join(Environment.NewLine, numbered);

                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            if (SelectedItem?.FullPath == path)
                            {
                                InspectorIsRtl = textFarsi.IsRightToLeft;
                                InspectorContent = text;
                            }
                        });
                    }
                    catch (Exception ex)
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            if (SelectedItem?.FullPath == path)
                                InspectorContent = $"Unable to preview: {ex.Message}";
                        });
                    }
                });
            }
            else
            {
                InspectorImage = null;
                InspectorContent = $"Binary file ({item.FormattedSize}). Preview not available.";
            }
        }
        else
        {
            InspectorImage = null;
            InspectorContent = item.IsDirectory ? $"Directory: {item.Name}" : string.Empty;
        }
    }

    public void SelectAll(bool select)
    {
        foreach (var item in DisplayItems)
        {
            item.IsSelected = select;
        }
        UpdateStatus();
    }

    private void UpdateStatus()
    {
        int total = _allItems.Count;
        int displayed = DisplayItems.Count;
        int selectedCount = DisplayItems.Count(x => x.IsSelected);
        int folders = DisplayItems.Count(x => x.IsDirectory);
        int files = displayed - folders;

        if (selectedCount > 0)
        {
            long selectedBytes = DisplayItems.Where(x => x.IsSelected && !x.IsDirectory).Sum(x => x.Size);
            string sizeStr = FileItem.FormatBytes(selectedBytes);
            StatusText = $"Selected: {selectedCount} items ({sizeStr}) | {displayed} items ({folders} folders, {files} files)";
        }
        else if (SelectedItem != null)
        {
            StatusText = $"{SelectedItem.Name} ({SelectedItem.FormattedSize}) | {displayed} items ({folders} folders, {files} files)";
        }
        else
        {
            StatusText = $"{displayed} items ({folders} folders, {files} files)";
        }
    }

    public void AddNewTab(string? path = null)
    {
        string p = path ?? ActiveTab?.CurrentPath ?? "C:\\";
        var tab = new PanelTab { CurrentPath = p };
        Tabs.Add(tab);
        ActiveTab = tab;
    }

    public void CloseTab(PanelTab tab)
    {
        if (Tabs.Count <= 1) return;
        int idx = Tabs.IndexOf(tab);
        Tabs.Remove(tab);
        if (ActiveTab == tab)
        {
            int newIdx = Math.Min(idx, Tabs.Count - 1);
            ActiveTab = Tabs[newIdx];
        }
    }

    public void GoBack()
    {
        if (ActiveTab?.CanGoBack == true)
        {
            string? target = ActiveTab.GoBack();
            if (target != null) NavigateToAsync(target, addToHistory: false);
        }
    }

    public void GoForward()
    {
        if (ActiveTab?.CanGoForward == true)
        {
            string? target = ActiveTab.GoForward();
            if (target != null) NavigateToAsync(target, addToHistory: false);
        }
    }

    public void GoUp()
    {
        if (ActiveTab == null) return;
        var parent = Directory.GetParent(ActiveTab.CurrentPath);
        if (parent != null && parent.Exists)
        {
            NavigateToAsync(parent.FullName);
        }
    }

    public void AddBookmark(string path)
    {
        if (Bookmarks.Any(b => b.Path.Equals(path, StringComparison.OrdinalIgnoreCase)))
            return;

        string name = Path.GetFileName(path.TrimEnd('\\'));
        if (string.IsNullOrEmpty(name)) name = path;

        Bookmarks.Add(new QuickAccessItem
        {
            Title = name,
            Path = path,
            Group = "Bookmarks",
            Icon = "⭐",
            HasFarsi = FarsiRecognitionEngine.ContainsFarsi(name),
            IsRemovable = true
        });
    }

    public void RemoveBookmark(QuickAccessItem item)
    {
        Bookmarks.Remove(item);
    }

    private void AddRecent(string path)
    {
        var existing = Recents.FirstOrDefault(r => r.Path.Equals(path, StringComparison.OrdinalIgnoreCase));
        if (existing != null) Recents.Remove(existing);

        string name = Path.GetFileName(path.TrimEnd('\\'));
        if (string.IsNullOrEmpty(name)) name = path;

        Recents.Insert(0, new QuickAccessItem
        {
            Title = name,
            Path = path,
            Group = "Recents",
            Icon = "🕒",
            HasFarsi = FarsiRecognitionEngine.ContainsFarsi(name),
            IsRemovable = true
        });

        while (Recents.Count > 15) Recents.RemoveAt(Recents.Count - 1);
    }

    public void CreateFolder()
    {
        if (ActiveTab == null) return;
        string parent = ActiveTab.CurrentPath;
        string baseName = "New Folder";
        string name = baseName;
        int i = 1;
        while (Directory.Exists(Path.Combine(parent, name)))
        {
            name = $"{baseName} ({i++})";
        }

        if (FileSystemService.CreateFolder(parent, name))
        {
            _ = RefreshCurrentDirectoryAsync();
            SelectedItem = DisplayItems.FirstOrDefault(x => x.Name == name);
        }
    }

    public void CreateFile()
    {
        if (ActiveTab == null) return;
        string parent = ActiveTab.CurrentPath;
        string baseName = "New Text Document.txt";
        string name = baseName;
        int i = 1;
        while (File.Exists(Path.Combine(parent, name)))
        {
            name = $"New Text Document ({i++}).txt";
        }

        if (FileSystemService.CreateFile(parent, name))
        {
            _ = RefreshCurrentDirectoryAsync();
            SelectedItem = DisplayItems.FirstOrDefault(x => x.Name == name);
        }
    }

    public void DeleteSelected(bool permanent = false)
    {
        if (SelectedItem == null) return;
        string path = SelectedItem.FullPath;
        string msg = permanent ? $"Permanently delete '{SelectedItem.Name}'?" : $"Move '{SelectedItem.Name}' to Recycle Bin?";

        if (MessageBox.Show(msg, "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
        {
            if (FileSystemService.Delete(new[] { path }, permanent))
            {
                _ = RefreshCurrentDirectoryAsync();
            }
        }
    }

    public void CopySelected()
    {
        if (SelectedItem == null) return;
        _clipboardPaths = new List<string> { SelectedItem.FullPath };
        _isClipboardCut = false;
    }

    public void CutSelected()
    {
        if (SelectedItem == null) return;
        _clipboardPaths = new List<string> { SelectedItem.FullPath };
        _isClipboardCut = true;
    }

    public void Paste()
    {
        if (ActiveTab == null || _clipboardPaths.Count == 0) return;
        string targetDir = ActiveTab.CurrentPath;

        foreach (var src in _clipboardPaths)
        {
            try
            {
                string fileName = Path.GetFileName(src);
                string dest = Path.Combine(targetDir, fileName);

                if (File.Exists(src))
                {
                    if (_isClipboardCut) File.Move(src, dest, true);
                    else File.Copy(src, dest, true);
                }
                else if (Directory.Exists(src))
                {
                    if (_isClipboardCut) Directory.Move(src, dest);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Paste error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        if (_isClipboardCut) _clipboardPaths.Clear();
        _ = RefreshCurrentDirectoryAsync();
    }

    private void InitializeDrivesAndPlaces()
    {
        Drives.Clear();
        foreach (var d in FileSystemService.GetDrives()) Drives.Add(d);

        Places.Clear();
        foreach (var p in FileSystemService.GetPlaces()) Places.Add(p);
    }

    private void InitializeCommands()
    {
        AllCommands.Add(new CommandItem { Id = "HK_StartFileFilter", Name = "Quick Filter / Search", Category = "View", Hotkey = "/ or Ctrl+F", Description = "Start filtering files in the current folder." });
        AllCommands.Add(new CommandItem { Id = "HK_ToggleExpandFolder", Name = "Toggle Expand Folder In-line", Category = "View", Hotkey = "Ctrl+E", Description = "Expand or collapse selected folder in tree view.", Action = () => _ = ToggleExpandFolderAsync(SelectedItem) });
        AllCommands.Add(new CommandItem { Id = "HK_FilterFarsi", Name = "Filter Farsi Files (Recognize Farsi)", Category = "Farsi", Hotkey = "Alt+F", Description = "Toggle filter to display only files with Persian/Farsi names.", Action = () => FilterFarsiOnly = !FilterFarsiOnly });
        AllCommands.Add(new CommandItem { Id = "HK_ToggleInspector", Name = "Toggle Inspector Panel", Category = "Inspector", Hotkey = "Ctrl+I", Description = "Show or hide the metadata and preview inspector.", Action = () => IsInspectorVisible = !IsInspectorVisible });
        AllCommands.Add(new CommandItem { Id = "HK_ToggleDualPanel", Name = "Toggle Dual Split Panel", Category = "Panels", Hotkey = "Ctrl+D", Description = "Split view into two panels.", Action = () => IsDualPanel = !IsDualPanel });
        AllCommands.Add(new CommandItem { Id = "HK_TabNew", Name = "New Tab", Category = "Tabs", Hotkey = "Ctrl+T", Description = "Open a new tab.", Action = () => AddNewTab() });
        AllCommands.Add(new CommandItem { Id = "HK_TabClose", Name = "Close Tab", Category = "Tabs", Hotkey = "Ctrl+W", Description = "Close current tab.", Action = () => { if (ActiveTab != null) CloseTab(ActiveTab); } });
        AllCommands.Add(new CommandItem { Id = "HK_CreateFolder", Name = "New Folder", Category = "Folder", Hotkey = "Ctrl+Shift+N", Description = "Create a new folder.", Action = CreateFolder });
        AllCommands.Add(new CommandItem { Id = "HK_CreateFile", Name = "New File", Category = "Folder", Hotkey = "Ctrl+N", Description = "Create a new empty text document.", Action = CreateFile });
        AllCommands.Add(new CommandItem { Id = "HK_SelectionTrash", Name = "Delete (Recycle Bin)", Category = "Misc", Hotkey = "Del", Description = "Move selected item to Recycle Bin.", Action = () => DeleteSelected(false) });
        AllCommands.Add(new CommandItem { Id = "HK_SelectionDelete", Name = "Permanently Delete", Category = "Misc", Hotkey = "Shift+Del", Description = "Permanently delete selected item.", Action = () => DeleteSelected(true) });
        AllCommands.Add(new CommandItem { Id = "HK_ExplorerOpen", Name = "Open In Windows Explorer", Category = "Misc", Hotkey = "Alt+E", Description = "Open current directory in Windows Explorer.", Action = () => { if (ActiveTab != null) FileSystemService.OpenInExplorer(ActiveTab.CurrentPath); } });
        AllCommands.Add(new CommandItem { Id = "HK_ConsoleOpen", Name = "Open In Terminal", Category = "Misc", Hotkey = "Ctrl+`", Description = "Open current directory in Terminal.", Action = () => { if (ActiveTab != null) FileSystemService.OpenInTerminal(ActiveTab.CurrentPath); } });
        AllCommands.Add(new CommandItem { Id = "HK_Refresh", Name = "Refresh", Category = "View", Hotkey = "F5", Description = "Refresh the file list.", Action = () => _ = RefreshCurrentDirectoryAsync() });
    }

    public void SaveSessionOnExit()
    {
        var session = new SessionFileModel();
        session.Layout.SplitDualView = IsDualPanel;
        session.Layout.SplitOrientation = SplitOrientation;
        session.Layout.InspectorVisible = IsInspectorVisible;
        session.Layout.FilterFarsiOnly = FilterFarsiOnly;

        if (ActiveTab != null)
        {
            session.Layout.Panels.Add(new SessionPanelModel
            {
                ID = 1,
                Path = ActiveTab.CurrentPath
            });
        }

        SessionManager.SaveSession(session);
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
