using System;

namespace Timer.Application.Interfaces;

public interface ILogService
{
    event EventHandler<string>? LogAppended;
    void LogError(string message, Exception ex);
    string GetLog();
}
