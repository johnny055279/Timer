using System;
using Timer.Application.Interfaces;

namespace Timer.Application.Services;

public sealed class CounterService : ICounterService
{
    private int _count;
    private int _step = 1;

    public int Count => _count;
    public int Step => _step;

    public void SetCount(int count)
    {
        _count = Math.Max(0, count);
    }

    public void SetStep(int step)
    {
        _step = step > 0 ? step : 1;
    }

    public void Increase()
    {
        _count += _step;
    }

    public void Decrease()
    {
        _count = Math.Max(0, _count - _step);
    }

    public void Reset()
    {
        _count = 0;
    }
}
