using System;
using System.Text;
using Timer.Application.Interfaces;

namespace Timer.Application.Services;

public sealed class InMemoryLogService : ILogService
{
    private readonly StringBuilder _log = new();

    public event EventHandler<string>? LogAppended;

    public void LogError(string message, Exception ex)
    {
        var entry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [ERROR] {message}{Environment.NewLine}{ex}{Environment.NewLine}";
        _log.Append(entry);
        LogAppended?.Invoke(this, entry);
    }

    public string GetLog()
    {
        return _log.ToString();
    }
}
