using System;
using Serilog.Core;
using Serilog.Events;

namespace PCRemote.Agent.Logging;

public sealed class LogNotificationSink : ILogEventSink
{
    private readonly Action _notify;

    public LogNotificationSink(Action notify)
    {
        _notify = notify;
    }

    public void Emit(LogEvent logEvent)
    {
        _notify();
    }
}