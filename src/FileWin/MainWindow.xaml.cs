using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using FileWin.Models;
using FileWin.Services;
using FileWin.ViewModels;

namespace FileWin;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm;

    public MainWindow()
    {
        InitializeComponent();
        _vm = new MainViewModel();
        DataContext = _vm;
    }

    // Title Bar dragging & Controls
    private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
        {
            if (e.ClickCount == 2)
            {
                WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
            }
            else
            {
                try
                {
                    DragMove();
                }
                catch { }
            }
        }
    }

    private void BtnMinimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void BtnMaximize_Click(object sender, RoutedEventArgs e) =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();

    private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        _vm.SaveSessionOnExit();
    }

    // Navigation buttons
    private void BtnBack_Click(object sender, RoutedEventArgs e) => _vm.GoBack();
    private void BtnForward_Click(object sender, RoutedEventArgs e) => _vm.GoForward();
    private void BtnUp_Click(object sender, RoutedEventArgs e) => _vm.GoUp();
    private void BtnRefresh_Click(object sender, RoutedEventArgs e) => _ = _vm.RefreshCurrentDirectoryAsync();

    private void TxtPath_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            string path = TxtPath.Text.Trim();
            if (Directory.Exists(path))
            {
                _vm.NavigateToAsync(path);
                GridFiles.Focus();
            }
            else
            {
                MessageBox.Show($"Directory not found: {path}", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }

    // Quick filter box keydown
    private void TxtFilter_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            _vm.FilterText = string.Empty;
            GridFiles.Focus();
            e.Handled = true;
        }
        else if (e.Key == Key.Down || e.Key == Key.Enter)
        {
            GridFiles.Focus();
            if (GridFiles.Items.Count > 0 && GridFiles.SelectedIndex < 0)
            {
                GridFiles.SelectedIndex = 0;
            }
            e.Handled = true;
        }
    }

    // In-Line Tree Expander Arrow click
    private void Expander_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is FileItem item)
        {
            _ = _vm.ToggleExpandFolderAsync(item);
            e.Handled = true;
        }
    }

    // Header Select All CheckBox
    private void ChkHeaderSelectAll_Click(object sender, RoutedEventArgs e)
    {
        if (sender is CheckBox chk)
        {
            _vm.SelectAll(chk.IsChecked == true);
        }
    }

    // Grid interaction
    private void GridFiles_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (_vm.SelectedItem is FileItem item)
        {
            if (item.IsDirectory)
            {
                _vm.NavigateToAsync(item.FullPath);
            }
            else
            {
                FileSystemService.OpenWithDefault(item.FullPath);
            }
        }
    }

    private void GridFiles_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            if (_vm.SelectedItem is FileItem item)
            {
                if (item.IsDirectory) _vm.NavigateToAsync(item.FullPath);
                else FileSystemService.OpenWithDefault(item.FullPath);
                e.Handled = true;
            }
        }
        else if (e.Key == Key.Back)
        {
            _vm.GoUp();
            e.Handled = true;
        }
        else if (e.Key == Key.Delete)
        {
            bool permanent = (Keyboard.Modifiers & ModifierKeys.Shift) != 0;
            _vm.DeleteSelected(permanent);
            e.Handled = true;
        }
        else if (e.Key == Key.F2)
        {
            MenuRename_Click(sender, e);
            e.Handled = true;
        }
    }

    private void DataGrid_Sorting(object sender, DataGridSortingEventArgs e)
    {
        e.Handled = true;
        string column = e.Column.SortMemberPath;
        _vm.SortByColumn(column);
    }

    // Tab buttons
    private void Tab_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is PanelTab tab)
        {
            _vm.ActiveTab = tab;
        }
    }

    private void BtnNewTab_Click(object sender, RoutedEventArgs e) => _vm.AddNewTab();

    private void BtnCloseTab_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is PanelTab tab)
        {
            _vm.CloseTab(tab);
        }
    }

    // Quick Access & Bookmarks
    private void Drive_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is DriveItem drive)
        {
            _vm.NavigateToAsync(drive.RootPath);
        }
    }

    private void QuickAccessItem_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is QuickAccessItem item)
        {
            _vm.NavigateToAsync(item.Path);
        }
    }

    private void BtnPinBookmark_Click(object sender, RoutedEventArgs e)
    {
        if (_vm.SelectedItem != null && _vm.SelectedItem.IsDirectory)
        {
            _vm.AddBookmark(_vm.SelectedItem.FullPath);
        }
        else if (_vm.ActiveTab != null)
        {
            _vm.AddBookmark(_vm.ActiveTab.CurrentPath);
        }
    }

    private void BtnRemoveBookmark_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is QuickAccessItem item)
        {
            _vm.RemoveBookmark(item);
        }
    }

    // Actions
    private void BtnNewFolder_Click(object sender, RoutedEventArgs e) => _vm.CreateFolder();
    private void BtnNewFile_Click(object sender, RoutedEventArgs e) => _vm.CreateFile();
    private void BtnPaste_Click(object sender, RoutedEventArgs e) => _vm.Paste();
    private void BtnInspector_Click(object sender, RoutedEventArgs e) => _vm.IsInspectorVisible = !_vm.IsInspectorVisible;

    // Context menu
    private void MenuOpen_Click(object sender, RoutedEventArgs e)
    {
        if (_vm.SelectedItem != null)
        {
            if (_vm.SelectedItem.IsDirectory) _vm.NavigateToAsync(_vm.SelectedItem.FullPath);
            else FileSystemService.OpenWithDefault(_vm.SelectedItem.FullPath);
        }
    }

    private void MenuExpand_Click(object sender, RoutedEventArgs e)
    {
        _ = _vm.ToggleExpandFolderAsync(_vm.SelectedItem);
    }

    private void MenuOpenExplorer_Click(object sender, RoutedEventArgs e)
    {
        if (_vm.SelectedItem != null) FileSystemService.OpenInExplorer(_vm.SelectedItem.FullPath);
        else if (_vm.ActiveTab != null) FileSystemService.OpenInExplorer(_vm.ActiveTab.CurrentPath);
    }

    private void MenuOpenTerminal_Click(object sender, RoutedEventArgs e)
    {
        if (_vm.SelectedItem != null && _vm.SelectedItem.IsDirectory)
            FileSystemService.OpenInTerminal(_vm.SelectedItem.FullPath);
        else if (_vm.ActiveTab != null)
            FileSystemService.OpenInTerminal(_vm.ActiveTab.CurrentPath);
    }

    private void MenuCopy_Click(object sender, RoutedEventArgs e) => _vm.CopySelected();
    private void MenuCut_Click(object sender, RoutedEventArgs e) => _vm.CutSelected();
    private void MenuDelete_Click(object sender, RoutedEventArgs e) => _vm.DeleteSelected(false);
    private void MenuPermanentDelete_Click(object sender, RoutedEventArgs e) => _vm.DeleteSelected(true);

    private void MenuRename_Click(object sender, RoutedEventArgs e)
    {
        if (_vm.SelectedItem == null) return;
        string currentName = _vm.SelectedItem.Name;
        string prompt = Microsoft.VisualBasic.Interaction.InputBox("Enter new name:", "Rename Item", currentName);
        if (!string.IsNullOrWhiteSpace(prompt) && prompt != currentName)
        {
            if (FileSystemService.Rename(_vm.SelectedItem.FullPath, prompt))
            {
                _ = _vm.RefreshCurrentDirectoryAsync();
            }
        }
    }

    // Command Palette Modal Handling
    private void BtnCommands_Click(object sender, RoutedEventArgs e) => ToggleCommandPalette();

    private void ToggleCommandPalette()
    {
        if (CommandPaletteOverlay.Visibility == Visibility.Visible)
        {
            CommandPaletteOverlay.Visibility = Visibility.Collapsed;
            GridFiles.Focus();
        }
        else
        {
            CommandPaletteOverlay.Visibility = Visibility.Visible;
            TxtCmdSearch.Text = string.Empty;
            UpdateCommandPaletteList();
            TxtCmdSearch.Focus();
        }
    }

    private void CommandPaletteOverlay_MouseDown(object sender, MouseButtonEventArgs e)
    {
        CommandPaletteOverlay.Visibility = Visibility.Collapsed;
        GridFiles.Focus();
    }

    private void CommandPaletteModal_MouseDown(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true; // prevent overlay closing
    }

    private void UpdateCommandPaletteList()
    {
        string q = TxtCmdSearch.Text.Trim();
        var matches = string.IsNullOrWhiteSpace(q)
            ? _vm.AllCommands.ToList()
            : _vm.AllCommands.Where(c => c.Name.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                                         c.Category.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                                         c.Description.Contains(q, StringComparison.OrdinalIgnoreCase)).ToList();

        ListCommands.ItemsSource = matches;
        if (matches.Count > 0) ListCommands.SelectedIndex = 0;
    }

    private void TxtCmdSearch_TextChanged(object sender, TextChangedEventArgs e) => UpdateCommandPaletteList();

    private void TxtCmdSearch_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            CommandPaletteOverlay.Visibility = Visibility.Collapsed;
            GridFiles.Focus();
            e.Handled = true;
        }
        else if (e.Key == Key.Down)
        {
            if (ListCommands.SelectedIndex < ListCommands.Items.Count - 1)
                ListCommands.SelectedIndex++;
            e.Handled = true;
        }
        else if (e.Key == Key.Up)
        {
            if (ListCommands.SelectedIndex > 0)
                ListCommands.SelectedIndex--;
            e.Handled = true;
        }
        else if (e.Key == Key.Enter)
        {
            ExecuteSelectedCommand();
            e.Handled = true;
        }
    }

    private void ListCommands_MouseDoubleClick(object sender, MouseButtonEventArgs e) => ExecuteSelectedCommand();

    private void ExecuteSelectedCommand()
    {
        if (ListCommands.SelectedItem is CommandItem cmd)
        {
            CommandPaletteOverlay.Visibility = Visibility.Collapsed;
            cmd.Action?.Invoke();
            GridFiles.Focus();
        }
    }

    // Global Key Down & Shortcuts
    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        // 1. Command palette toggle (Ctrl+P or F1)
        if (((Keyboard.Modifiers & ModifierKeys.Control) != 0 && e.Key == Key.P) || e.Key == Key.F1)
        {
            ToggleCommandPalette();
            e.Handled = true;
            return;
        }

        // If command palette is open, handle Esc
        if (CommandPaletteOverlay.Visibility == Visibility.Visible)
        {
            if (e.Key == Key.Escape)
            {
                CommandPaletteOverlay.Visibility = Visibility.Collapsed;
                GridFiles.Focus();
                e.Handled = true;
            }
            return;
        }

        // Check if user is currently typing in an editable TextBox
        bool isEditingText = Keyboard.FocusedElement is TextBox;

        // 2. Shortcut: '/' (Slash) focuses search/filter immediately
        if (!isEditingText && (e.Key == Key.Oem2 || e.Key == Key.Divide))
        {
            TxtFilter.Focus();
            TxtFilter.SelectAll();
            e.Handled = true;
            return;
        }

        // 3. Shortcut: Ctrl + E to Expand/Collapse folder in-line in tree view
        if ((Keyboard.Modifiers & ModifierKeys.Control) != 0 && e.Key == Key.E)
        {
            _ = _vm.ToggleExpandFolderAsync(_vm.SelectedItem);
            e.Handled = true;
            return;
        }

        // 4. Other Global Shortcuts
        if ((Keyboard.Modifiers & ModifierKeys.Alt) != 0)
        {
            if (e.Key == Key.F)
            {
                _vm.FilterFarsiOnly = !_vm.FilterFarsiOnly;
                e.Handled = true;
            }
            else if (e.Key == Key.Left)
            {
                _vm.GoBack();
                e.Handled = true;
            }
            else if (e.Key == Key.Right)
            {
                _vm.GoForward();
                e.Handled = true;
            }
            else if (e.Key == Key.Up)
            {
                _vm.GoUp();
                e.Handled = true;
            }
            else if (e.Key == Key.E)
            {
                MenuOpenExplorer_Click(sender, e);
                e.Handled = true;
            }
        }
        else if ((Keyboard.Modifiers & ModifierKeys.Control) != 0)
        {
            if (e.Key == Key.F)
            {
                TxtFilter.Focus();
                TxtFilter.SelectAll();
                e.Handled = true;
            }
            else if (e.Key == Key.T)
            {
                _vm.AddNewTab();
                e.Handled = true;
            }
            else if (e.Key == Key.W)
            {
                if (_vm.ActiveTab != null) _vm.CloseTab(_vm.ActiveTab);
                e.Handled = true;
            }
            else if (e.Key == Key.I)
            {
                _vm.IsInspectorVisible = !_vm.IsInspectorVisible;
                e.Handled = true;
            }
            else if (e.Key == Key.N)
            {
                if ((Keyboard.Modifiers & ModifierKeys.Shift) != 0) _vm.CreateFolder();
                else _vm.CreateFile();
                e.Handled = true;
            }
            else if (e.Key == Key.C && !isEditingText)
            {
                _vm.CopySelected();
                e.Handled = true;
            }
            else if (e.Key == Key.X && !isEditingText)
            {
                _vm.CutSelected();
                e.Handled = true;
            }
            else if (e.Key == Key.V && !isEditingText)
            {
                _vm.Paste();
                e.Handled = true;
            }
        }
        else if (e.Key == Key.F5)
        {
            _ = _vm.RefreshCurrentDirectoryAsync();
            e.Handled = true;
        }
    }
}
