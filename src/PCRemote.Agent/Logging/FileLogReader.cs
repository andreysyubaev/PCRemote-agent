
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using PCRemote.Core.Interfaces.Logging;
using PCRemote.Core.Models.Logging;

namespace PCRemote.Agent.Logging;

public class FileLogReader : ILogReader
{
    private static readonly Regex LogEntryRegex = new(
        @"^(?<timestamp>\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d{3}) \[(?<level>[A-Z]{3})\] \[(?<source>[^\]]+)\] (?<message>.*)$",
        RegexOptions.Compiled);

    private readonly string _logsDirectory;

    public FileLogReader()
    {
        _logsDirectory = Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData),
            "PCRemote",
            "Logs");
    }

    public async Task<IReadOnlyList<LogEntry>> ReadAllAsync(
        CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(_logsDirectory))
            return Array.Empty<LogEntry>();

        var files = Directory.GetFiles(
            _logsDirectory,
            "pcremote-*.log");

        var entries = new List<LogEntry>();

        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var lines = await File.ReadAllLinesAsync(
                file,
                cancellationToken);

            LogEntry? currentEntry = null;
            var exceptionBuilder = new StringBuilder();

            foreach (var line in lines)
            {
                var match = LogEntryRegex.Match(line);

                if (match.Success)
                {
                    AddCurrentEntry();

                    if (!DateTime.TryParseExact(
                            match.Groups["timestamp"].Value,
                            "yyyy-MM-dd HH:mm:ss.fff",
                            CultureInfo.InvariantCulture,
                            DateTimeStyles.None,
                            out var timestamp))
                    {
                        currentEntry = null;
                        continue;
                    }

                    currentEntry = new LogEntry
                    {
                        Timestamp = timestamp,
                        Level = match.Groups["level"].Value,
                        Source = match.Groups["source"].Value,
                        Message = match.Groups["message"].Value
                    };
                }
                else if (currentEntry is not null)
                {
                    exceptionBuilder.AppendLine(line);
                }
            }

            AddCurrentEntry();

            void AddCurrentEntry()
            {
                if (currentEntry is null)
                    return;

                var exception = exceptionBuilder.ToString().TrimEnd();

                entries.Add(new LogEntry
                {
                    Timestamp = currentEntry.Timestamp,
                    Level = currentEntry.Level,
                    Source = currentEntry.Source,
                    Message = currentEntry.Message,
                    Exception = string.IsNullOrWhiteSpace(exception)
                        ? null
                        : exception
                });

                currentEntry = null;
                exceptionBuilder.Clear();
            }
        }

        return entries
            .OrderBy(entry => entry.Timestamp)
            .ToList();
    }
}
