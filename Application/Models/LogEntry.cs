using System;

namespace Timer.Application.Models;

public sealed record LogEntry(DateTimeOffset Timestamp, string Level, string Text);
