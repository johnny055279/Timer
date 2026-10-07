using System;
using System.Collections.Generic;
using Timer.Application.Models;

namespace Timer.Application.Interfaces;

public interface ILogService
{
    event EventHandler<string>? LogAppended;
    string LogDirectory { get; }
    void LogInfo(string message);
    void LogWarning(string message);
    void LogError(string message, Exception ex);
    string GetLog();
    IReadOnlyList<LogEntry> GetEntries();
}
