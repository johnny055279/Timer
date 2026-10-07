using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Timer.Application.Interfaces;
using Timer.Application.Models;

namespace Timer.Infrastructure.Logging;

// Keeps the most recent entries in memory for the Debug window / upload and
// appends every entry to a daily file, so a streamer can still send the log
// after the app has been restarted.
public sealed class FileLogService : ILogService
{
    private const int MaxEntries = 2000;
    private const int RetentionDays = 7;
    private static readonly string DefaultDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Timer",
        "logs");

    private readonly object _gate = new();
    private readonly Queue<LogEntry> _entries = new();

    public FileLogService()
        : this(DefaultDirectory)
    {
    }

    public FileLogService(string directory)
    {
        LogDirectory = directory;
        TryDeleteOldFiles();
    }

    public event EventHandler<string>? LogAppended;

    public string LogDirectory { get; }

    public void LogInfo(string message) => Append("INFO", message);

    public void LogWarning(string message) => Append("WARN", message);

    public void LogError(string message, Exception ex) => Append("ERROR", $"{message}{Environment.NewLine}{ex}");

    public string GetLog()
    {
        lock (_gate)
        {
            return string.Concat(_entries.Select(Format));
        }
    }

    public IReadOnlyList<LogEntry> GetEntries()
    {
        lock (_gate)
        {
            return _entries.ToArray();
        }
    }

    private void Append(string level, string text)
    {
        var entry = new LogEntry(DateTimeOffset.Now, level, text);
        var line = Format(entry);
        lock (_gate)
        {
            _entries.Enqueue(entry);
            while (_entries.Count > MaxEntries)
            {
                _entries.Dequeue();
            }

            TryAppendToFile(entry, line);
        }

        LogAppended?.Invoke(this, line);
    }

    private static string Format(LogEntry entry)
    {
        return $"[{entry.Timestamp:yyyy-MM-dd HH:mm:ss}] [{entry.Level}] {entry.Text}{Environment.NewLine}";
    }

    private void TryAppendToFile(LogEntry entry, string line)
    {
        try
        {
            Directory.CreateDirectory(LogDirectory);
            File.AppendAllText(Path.Combine(LogDirectory, $"timer-{entry.Timestamp:yyyyMMdd}.log"), line);
        }
        catch
        {
            // Logging must never take the app down; the in-memory copy still works.
        }
    }

    private void TryDeleteOldFiles()
    {
        try
        {
            if (!Directory.Exists(LogDirectory))
            {
                return;
            }

            var cutoff = DateTime.Now.AddDays(-RetentionDays);
            foreach (var file in Directory.EnumerateFiles(LogDirectory, "timer-*.log"))
            {
                if (File.GetLastWriteTime(file) < cutoff)
                {
                    File.Delete(file);
                }
            }
        }
        catch
        {
            // Best-effort cleanup.
        }
    }
}
