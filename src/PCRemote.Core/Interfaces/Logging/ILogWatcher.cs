using System;

namespace PCRemote.Core.Interfaces.Logging;

public interface ILogWatcher
{
    event EventHandler? LogsChanged;
}