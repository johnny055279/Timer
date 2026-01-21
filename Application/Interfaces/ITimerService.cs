using System;

namespace Timer.Application.Interfaces;

public interface ITimerService
{
    TimeSpan Remaining { get; }
    bool IsPaused { get; }
    bool CanPlay { get; }

    void AdjustMinutes(int minutes);
    void Reset();
    void TogglePause();
    bool Tick(DateTimeOffset now);
}
