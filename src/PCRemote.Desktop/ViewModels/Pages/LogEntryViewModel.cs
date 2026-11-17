using PCRemote.Core.Models.Logging;

namespace PCRemote.Desktop.ViewModels.Pages;

public sealed class LogEntryViewModel
{
    private const string NeutralIcon = "/Assets/Icons/eye-outline.svg";
    private const string DebugIcon = "/Assets/Icons/debug-outline.svg";
    private const string InfoIcon = "/Assets/Icons/info-outline.svg";
    private const string WarningIcon = "/Assets/Icons/alert-triangle-outline.svg";
    private const string DangerIcon = "/Assets/Icons/close-circle-outline.svg";
    private const string FatalIcon = "/Assets/Icons/skull-outline.svg";

    public LogEntryViewModel(LogEntry entry)
    {
        Message = entry.Message;
        Source = entry.Source;
        TimestampText = entry.Timestamp.ToString("yyyy-MM-dd HH:mm:ss");

        (IconPath, IsDanger, IsInfo, IsNeutral, IsDebug, IsWarning, IsFatal) =
            MapLevel(entry.Level);
    }

    public string Message { get; }

    public string Source { get; }

    public string TimestampText { get; }

    public string IconPath { get; }

    public bool IsDanger { get; }

    public bool IsInfo { get; }

    public bool IsNeutral { get; }

    public bool IsDebug { get; }

    public bool IsWarning { get; }

    public bool IsFatal { get; }

    private static (
        string IconPath,
        bool IsDanger,
        bool IsInfo,
        bool IsNeutral,
        bool IsDebug,
        bool IsWarning,
        bool IsFatal) MapLevel(string level)
    {
        return level switch
        {
            "DBG" => (DebugIcon, false, false, false, true, false, false),
            "INF" => (InfoIcon, false, true, false, false, false, false),
            "WRN" => (WarningIcon, false, false, false, false, true, false),
            "ERR" => (DangerIcon, true, false, false, false, false, false),
            "FTL" => (FatalIcon, false, false, false, false, false, true),
            _ => (NeutralIcon, false, false, true, false, false, false)
        };
    }
}
