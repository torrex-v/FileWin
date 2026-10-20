using System;
using System.IO;
using System.Threading;

namespace FileWin.Services;

public class DirectoryWatcherService : IDisposable
{
    private FileSystemWatcher? _watcher;
    private readonly Action _onChanged;
    private System.Threading.Timer? _debounceTimer;
    private readonly object _lock = new();

    public DirectoryWatcherService(Action onChanged)
    {
        _onChanged = onChanged;
    }

    public void WatchDirectory(string path)
    {
        lock (_lock)
        {
            Stop();
            if (!Directory.Exists(path)) return;

            try
            {
                _watcher = new FileSystemWatcher(path)
                {
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName |
                                   NotifyFilters.Size | NotifyFilters.LastWrite,
                    IncludeSubdirectories = false,
                    EnableRaisingEvents = true
                };

                _watcher.Created += HandleEvent;
                _watcher.Deleted += HandleEvent;
                _watcher.Renamed += HandleEvent;
                _watcher.Changed += HandleEvent;
            }
            catch { }
        }
    }

    private void HandleEvent(object sender, FileSystemEventArgs e)
    {
        lock (_lock)
        {
            _debounceTimer?.Dispose();
            _debounceTimer = new System.Threading.Timer(_ =>
            {
                _onChanged?.Invoke();
            }, null, 250, Timeout.Infinite);
        }
    }

    public void Stop()
    {
        lock (_lock)
        {
            if (_watcher != null)
            {
                try
                {
                    _watcher.EnableRaisingEvents = false;
                    _watcher.Dispose();
                }
                catch { }
                _watcher = null;
            }
            _debounceTimer?.Dispose();
            _debounceTimer = null;
        }
    }

    public void Dispose()
    {
        Stop();
    }
}
