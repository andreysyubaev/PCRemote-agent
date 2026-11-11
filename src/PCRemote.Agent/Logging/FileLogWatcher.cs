
using PCRemote.Core.Interfaces.Logging;

namespace PCRemote.Agent.Logging;

public class FileLogWatcher : ILogWatcher, IDisposable
{
    private readonly FileSystemWatcher _watcher;

    public event EventHandler? LogsChanged;
    public void NotifyLogsChanged()
    {
        LogsChanged?.Invoke(this, EventArgs.Empty);
    }
    
    public FileLogWatcher()
    {
        var logsDirectory = Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData),
            "PCRemote",
            "Logs");

        Directory.CreateDirectory(logsDirectory);

        _watcher = new FileSystemWatcher(
            logsDirectory,
            "pcremote-*.log")
        {
            NotifyFilter = NotifyFilters.LastWrite |
                           NotifyFilters.Size |
                           NotifyFilters.FileName |
                           NotifyFilters.LastAccess |
                           NotifyFilters.CreationTime,
            EnableRaisingEvents = false
        };

        _watcher.Changed += OnLogsChanged;
        _watcher.Created += OnLogsChanged;
        _watcher.Renamed += OnLogsRenamed;
        _watcher.Error += OnWatcherError;

        _watcher.EnableRaisingEvents = true;

        Console.WriteLine($"[LogWatcher] Watching: {logsDirectory}");
    }

    private void OnLogsChanged(object sender, FileSystemEventArgs e)
    {
        Console.WriteLine(
            $"[LogWatcher] File changed: {e.Name}");

        LogsChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnLogsRenamed(object sender, RenamedEventArgs e)
    {
        Console.WriteLine(
            $"[LogWatcher] File renamed: {e.Name}");

        LogsChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnWatcherError(object sender, ErrorEventArgs e)
    {
        Console.WriteLine(
            $"[LogWatcher] Error: {e.GetException()}");
    }

    public void Dispose()
    {
        _watcher.EnableRaisingEvents = false;

        _watcher.Changed -= OnLogsChanged;
        _watcher.Created -= OnLogsChanged;
        _watcher.Renamed -= OnLogsRenamed;
        _watcher.Error -= OnWatcherError;

        _watcher.Dispose();
    }
}
