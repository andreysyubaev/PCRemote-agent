
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PCRemote.Core.Interfaces.Logging;
using PCRemote.Core.Models.Logging;

namespace PCRemote.Desktop.ViewModels.Pages;

public partial class LogsPageViewModel : ViewModelBase, IDisposable
{
    private readonly ILogReader _logReader;
    private readonly ILogWatcher _logWatcher;

    private bool _reloadRequested;

    public ObservableCollection<LogEntry> Entries { get; } = new();

    public ObservableCollection<LogEntry> FilteredEntries { get; } = new();

    public ObservableCollection<string> Levels { get; } =
    [
        "All levels",
        "VRB",
        "DBG",
        "INF",
        "WRN",
        "ERR",
        "FTL"
    ];

    public ObservableCollection<string> Sources { get; } =
    [
        "All sources"
    ];

    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    [ObservableProperty]
    public partial string? ErrorMessage { get; set; }

    [ObservableProperty]
    public partial string SearchText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string SelectedLevel { get; set; } = "All levels";

    [ObservableProperty]
    public partial string SelectedSource { get; set; } = "All sources";

    public LogsPageViewModel(
        ILogReader logReader,
        ILogWatcher logWatcher)
    {
        _logReader = logReader;
        _logWatcher = logWatcher;

        _logWatcher.LogsChanged += OnLogsChanged;
    }

    partial void OnSearchTextChanged(string value)
    {
        ApplyFilters();
    }

    partial void OnSelectedLevelChanged(string value)
    {
        ApplyFilters();
    }

    partial void OnSelectedSourceChanged(string value)
    {
        ApplyFilters();
    }

    private void OnLogsChanged(object? sender, EventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            _ = LoadLogsCommand.ExecuteAsync(null);
        });
    }

    [RelayCommand]
    private async Task LoadLogsAsync()
    {
        if (IsLoading)
        {
            _reloadRequested = true;
            return;
        }

        try
        {
            IsLoading = true;
            ErrorMessage = null;

            do
            {
                _reloadRequested = false;

                var entries = await _logReader.ReadAllAsync();

                UpdateEntries(entries);

            } while (_reloadRequested);
        }
        catch (Exception)
        {
            ErrorMessage = "Не удалось загрузить журналы событий.";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void UpdateEntries(IReadOnlyList<LogEntry> newEntries)
    {
        var sameHistory = Entries.Count <= newEntries.Count;

        if (sameHistory)
        {
            for (var i = 0; i < Entries.Count; i++)
            {
                if (!AreEqual(Entries[i], newEntries[i]))
                {
                    sameHistory = false;
                    break;
                }
            }
        }

        if (sameHistory)
        {
            for (var i = Entries.Count; i < newEntries.Count; i++)
            {
                Entries.Add(newEntries[i]);
            }
        }
        else
        {
            Entries.Clear();

            foreach (var entry in newEntries)
            {
                Entries.Add(entry);
            }
        }

        RefreshSources();
        ApplyFilters();
    }

    private void RefreshSources()
    {
        var currentSource = SelectedSource;

        var availableSources = Entries
            .Select(entry => entry.Source)
            .Distinct()
            .OrderBy(source => source)
            .ToList();

        Sources.Clear();
        Sources.Add("All sources");

        foreach (var source in availableSources)
        {
            Sources.Add(source);
        }

        if (currentSource != "All sources" &&
            Sources.Contains(currentSource))
        {
            SelectedSource = currentSource;
        }
        else
        {
            SelectedSource = "All sources";
        }
    }

    private void ApplyFilters()
    {
        FilteredEntries.Clear();

        var search = SearchText.Trim();

        foreach (var entry in Entries)
        {
            if (SelectedLevel != "All levels" &&
                entry.Level != SelectedLevel)
            {
                continue;
            }

            if (SelectedSource != "All sources" &&
                entry.Source != SelectedSource)
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var matches =
                    entry.Message.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    entry.Source.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    (entry.Exception?.Contains(
                        search,
                        StringComparison.OrdinalIgnoreCase) ?? false);

                if (!matches)
                    continue;
            }

            FilteredEntries.Add(entry);
        }
    }

    [RelayCommand]
    private void ResetFilters()
    {
        SearchText = string.Empty;
        SelectedLevel = "All levels";
        SelectedSource = "All sources";
    }

    private static bool AreEqual(LogEntry first, LogEntry second)
    {
        return first.Timestamp == second.Timestamp
               && first.Level == second.Level
               && first.Source == second.Source
               && first.Message == second.Message
               && first.Exception == second.Exception;
    }

    public async Task ExportLogsAsync(Stream stream)
    {
        await using var writer = new StreamWriter(
            stream,
            leaveOpen: true);

        foreach (var entry in Entries)
        {
            await writer.WriteLineAsync(
                $"[{entry.Timestamp:yyyy-MM-dd HH:mm:ss.fff}] " +
                $"[{entry.Level}] " +
                $"[{entry.Source}] " +
                entry.Message);

            if (!string.IsNullOrWhiteSpace(entry.Exception))
            {
                await writer.WriteLineAsync(entry.Exception);
            }

            await writer.WriteLineAsync();
        }

        await writer.FlushAsync();
    }

    public void Dispose()
    {
        _logWatcher.LogsChanged -= OnLogsChanged;
    }
}
