namespace Timer.Application.Models;

public sealed class AppSettings
{
    public string CountdownTitle { get; set; } = string.Empty;
    public int CounterValue { get; set; }
    public int CounterStep { get; set; }
}
