using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using FileWin.Models;

namespace FileWin.Services;

public class FileSystemService
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct SHFILEOPSTRUCT
    {
        public IntPtr hwnd;
        public uint wFunc;
        [MarshalAs(UnmanagedType.LPTStr)]
        public string pFrom;
        [MarshalAs(UnmanagedType.LPTStr)]
        public string? pTo;
        public ushort fFlags;
        public bool fAnyOperationsAborted;
        public IntPtr hNameMappings;
        [MarshalAs(UnmanagedType.LPTStr)]
        public string? lpszProgressTitle;
    }

    private const uint FO_DELETE = 0x0003;
    private const ushort FOF_ALLOWUNDO = 0x0040;
    private const ushort FOF_NOCONFIRMATION = 0x0010;
    private const ushort FOF_SILENT = 0x0004;

    [DllImport("shell32.dll", CharSet = CharSet.Auto)]
    private static extern int SHFileOperation(ref SHFILEOPSTRUCT FileOp);

    public static List<FileItem> GetDirectoryItems(string path, bool showHidden = true, bool showSystem = true)
    {
        var result = new List<FileItem>();
        if (!Directory.Exists(path)) return result;

        try
        {
            var dirInfo = new DirectoryInfo(path);
            var entries = dirInfo.EnumerateFileSystemInfos();

            foreach (var entry in entries)
            {
                bool isHidden = (entry.Attributes & FileAttributes.Hidden) != 0;
                bool isSystem = (entry.Attributes & FileAttributes.System) != 0;

                if (!showHidden && isHidden) continue;
                if (!showSystem && isSystem) continue;

                result.Add(FileItem.FromFileSystemInfo(entry));
            }
        }
        catch (UnauthorizedAccessException) { }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error reading {path}: {ex.Message}");
        }

        return result;
    }

    public static List<DriveItem> GetDrives()
    {
        var list = new List<DriveItem>();
        try
        {
            foreach (var d in DriveInfo.GetDrives())
            {
                list.Add(DriveItem.FromDriveInfo(d));
            }
        }
        catch { }
        return list;
    }

    public static List<QuickAccessItem> GetPlaces()
    {
        var places = new List<QuickAccessItem>();

        void AddPlace(string title, Environment.SpecialFolder folder, string icon)
        {
            try
            {
                string path = Environment.GetFolderPath(folder);
                if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
                {
                    places.Add(new QuickAccessItem
                    {
                        Title = title,
                        Path = path,
                        Group = "Places",
                        Icon = icon,
                        HasFarsi = FarsiRecognitionEngine.ContainsFarsi(title)
                    });
                }
            }
            catch { }
        }

        AddPlace("Desktop", Environment.SpecialFolder.Desktop, "🖥️");
        AddPlace("Downloads", Environment.SpecialFolder.UserProfile, "📥"); // Will adjust path to Downloads
        // Explicit Downloads check
        string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string downloads = Path.Combine(userProfile, "Downloads");
        if (Directory.Exists(downloads))
        {
            var dlItem = places.FirstOrDefault(p => p.Title == "Downloads");
            if (dlItem != null) dlItem.Path = downloads;
        }

        AddPlace("Documents", Environment.SpecialFolder.MyDocuments, "📑");
        AddPlace("Pictures", Environment.SpecialFolder.MyPictures, "🖼️");
        AddPlace("Music", Environment.SpecialFolder.MyMusic, "🎵");
        AddPlace("Videos", Environment.SpecialFolder.MyVideos, "🎬");
        AddPlace("User Profile", Environment.SpecialFolder.UserProfile, "👤");

        return places;
    }

    public static async Task<(long Size, int Files, int Folders)> CalculateFolderSizeAsync(string folderPath, CancellationToken token)
    {
        return await Task.Run(() =>
        {
            long totalSize = 0;
            int fileCount = 0;
            int folderCount = 0;

            try
            {
                var stack = new Stack<string>();
                stack.Push(folderPath);

                while (stack.Count > 0 && !token.IsCancellationRequested)
                {
                    string current = stack.Pop();

                    try
                    {
                        var dir = new DirectoryInfo(current);
                        foreach (var f in dir.EnumerateFiles())
                        {
                            if (token.IsCancellationRequested) break;
                            totalSize += f.Length;
                            fileCount++;
                        }

                        foreach (var d in dir.EnumerateDirectories())
                        {
                            if (token.IsCancellationRequested) break;
                            if ((d.Attributes & FileAttributes.ReparsePoint) == 0) // avoid infinite loops
                            {
                                folderCount++;
                                stack.Push(d.FullName);
                            }
                        }
                    }
                    catch { }
                }
            }
            catch { }

            return (totalSize, fileCount, folderCount);
        }, token);
    }

    public static bool CreateFolder(string parentPath, string name)
    {
        try
        {
            string newPath = Path.Combine(parentPath, name);
            if (!Directory.Exists(newPath))
            {
                Directory.CreateDirectory(newPath);
                return true;
            }
        }
        catch { }
        return false;
    }

    public static bool CreateFile(string parentPath, string name)
    {
        try
        {
            string newPath = Path.Combine(parentPath, name);
            if (!File.Exists(newPath))
            {
                using var fs = File.Create(newPath);
                return true;
            }
        }
        catch { }
        return false;
    }

    public static bool Rename(string currentPath, string newName)
    {
        try
        {
            string? dir = Path.GetDirectoryName(currentPath);
            if (string.IsNullOrEmpty(dir)) return false;

            string targetPath = Path.Combine(dir, newName);
            if (Directory.Exists(currentPath))
            {
                Directory.Move(currentPath, targetPath);
                return true;
            }
            else if (File.Exists(currentPath))
            {
                File.Move(currentPath, targetPath);
                return true;
            }
        }
        catch { }
        return false;
    }

    public static bool Delete(IEnumerable<string> paths, bool permanent = false)
    {
        try
        {
            if (permanent)
            {
                foreach (var path in paths)
                {
                    if (Directory.Exists(path)) Directory.Delete(path, true);
                    else if (File.Exists(path)) File.Delete(path);
                }
                return true;
            }
            else
            {
                // Send to Recycle Bin via SHFileOperation
                string nullTerminatedList = string.Join("\0", paths) + "\0\0";
                var op = new SHFILEOPSTRUCT
                {
                    wFunc = FO_DELETE,
                    pFrom = nullTerminatedList,
                    fFlags = FOF_ALLOWUNDO | FOF_NOCONFIRMATION | FOF_SILENT
                };
                int ret = SHFileOperation(ref op);
                return ret == 0;
            }
        }
        catch { }
        return false;
    }

    public static void OpenWithDefault(string path)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true
            });
        }
        catch { }
    }

    public static void OpenInExplorer(string path)
    {
        try
        {
            string target = Directory.Exists(path) ? path : (Path.GetDirectoryName(path) ?? path);
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"\"{target}\"",
                UseShellExecute = true
            });
        }
        catch { }
    }

    public static void OpenInTerminal(string folderPath)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "wt.exe",
                Arguments = $"-d \"{folderPath}\"",
                UseShellExecute = true
            });
        }
        catch
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    WorkingDirectory = folderPath,
                    UseShellExecute = true
                });
            }
            catch { }
        }
    }

    public static string ComputeSha256(string filePath)
    {
        try
        {
            if (!File.Exists(filePath)) return string.Empty;
            var fi = new FileInfo(filePath);
            if (fi.Length > 100 * 1024 * 1024) return "File too large (>100MB)";

            using var sha256 = SHA256.Create();
            using var stream = File.OpenRead(filePath);
            byte[] hash = sha256.ComputeHash(stream);
            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}";
        }
    }
}
