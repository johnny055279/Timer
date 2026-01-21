using System;
using Timer.Application.Interfaces;

namespace Timer.Application.Services;

public sealed class CountdownTimerService : ITimerService
{
    private static readonly TimeSpan MaxDuration = TimeSpan.FromHours(999);
    private DateTimeOffset? _endTimeUtc;
    private TimeSpan _remaining = TimeSpan.Zero;
    private bool _isPaused;
    private bool _canPlay;

    public TimeSpan Remaining => _remaining;
    public bool IsPaused => _isPaused;
    public bool CanPlay => _canPlay;

    public void AdjustMinutes(int minutes)
    {
        var delta = TimeSpan.FromMinutes(minutes);
        if (_isPaused || _endTimeUtc is null)
        {
            _remaining = _remaining.Add(delta);
        }
        else
        {
            _endTimeUtc = _endTimeUtc.Value.Add(delta);
            _remaining = _endTimeUtc.Value - DateTimeOffset.UtcNow;
        }

        ClampRemaining();

        _canPlay = _remaining > TimeSpan.Zero;
    }

    public void Reset()
    {
        _remaining = TimeSpan.Zero;
        _endTimeUtc = null;
        _canPlay = false;
    }

    public void TogglePause()
    {
        _isPaused = !_isPaused;
        if (_isPaused)
        {
            if (_endTimeUtc is not null)
            {
                _remaining = _endTimeUtc.Value - DateTimeOffset.UtcNow;
                if (_remaining < TimeSpan.Zero)
                {
                    _remaining = TimeSpan.Zero;
                }
                _endTimeUtc = null;
            }
        }
        else
        {
            if (_remaining > TimeSpan.Zero)
            {
                _endTimeUtc = DateTimeOffset.UtcNow.Add(_remaining);
                _canPlay = true;
            }
        }

        ClampRemaining();
    }

    public bool Tick(DateTimeOffset now)
    {
        if (_isPaused || _endTimeUtc is null)
        {
            return false;
        }

        _remaining = _endTimeUtc.Value - now;
        if (_remaining <= TimeSpan.Zero)
        {
            _remaining = TimeSpan.Zero;
            _endTimeUtc = null;
            var shouldPlay = _canPlay;
            _canPlay = false;
            return shouldPlay;
        }

        return false;
    }

    private void ClampRemaining()
    {
        if (_remaining <= TimeSpan.Zero)
        {
            _remaining = TimeSpan.Zero;
            _endTimeUtc = null;
            _canPlay = false;
            return;
        }

        if (_remaining > MaxDuration)
        {
            _remaining = MaxDuration;
        }

        if (!_isPaused)
        {
            _endTimeUtc = DateTimeOffset.UtcNow.Add(_remaining);
        }
    }
}
