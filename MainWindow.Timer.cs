using System;
using System.Threading;
using System.Windows;
using System.Windows.Controls;

namespace Timer;

public partial class MainWindow
{
    private void OnTimerTick(object? sender, EventArgs e)
    {
        ApplyPendingMinutes();
        var shouldPlay = _timerService.Tick(DateTimeOffset.UtcNow);
        UpdateTimeDisplay();
        if (shouldPlay)
        {
            PlaySelectedBeep();
        }
    }

    private void UpdateTimeDisplay()
    {
        var remaining = _timerService.Remaining;
        TimeDisplayTextBlock.Text = $"{(int)remaining.TotalHours:00}:{remaining.Minutes:00}:{remaining.Seconds:00}";
    }

    private int GetStepMinutes()
    {
        if (!int.TryParse(StepMinutesTextBox.Text, out var minutes))
        {
            return MinStepMinutes;
        }

        return Clamp(minutes, MinStepMinutes, MaxStepMinutes);
    }

    private int GetCounterStep()
    {
        if (!int.TryParse(CounterStepTextBox.Text, out var step))
        {
            return MinCounterStep;
        }

        return Clamp(step, MinCounterStep, MaxCounterStep);
    }

    private void AdjustTime(int minutes)
    {
        _timerService.AdjustMinutes(minutes);
        UpdateTimeDisplay();
    }

    private void IncreaseTime_Click(object sender, RoutedEventArgs e)
    {
        AdjustTime(GetStepMinutes());
    }

    private void DecreaseTime_Click(object sender, RoutedEventArgs e)
    {
        AdjustTime(-GetStepMinutes());
    }

    private void ResetCountdown_Click(object sender, RoutedEventArgs e)
    {
        _timerService.Reset();
        UpdateTimeDisplay();
    }

    private void Pause_Click(object sender, RoutedEventArgs e)
    {
        _timerService.TogglePause();
        PauseButton.Content = _timerService.IsPaused ? "Resume (繼續)" : "Pause (暫停)";
        UpdateTimeDisplay();
    }

    private void IncreaseCounter_Click(object sender, RoutedEventArgs e)
    {
        _counterService.Increase();
        UpdateCounterDisplay();
        SaveSettings();
    }

    private void DecreaseCounter_Click(object sender, RoutedEventArgs e)
    {
        _counterService.Decrease();
        UpdateCounterDisplay();
        SaveSettings();
    }

    private void ResetCounter_Click(object sender, RoutedEventArgs e)
    {
        _counterService.Reset();
        UpdateCounterDisplay();
        SaveSettings();
    }

    private void UpdateCounterDisplay()
    {
        CounterValueTextBlock.Text = _counterService.Count.ToString();
    }

    private void CounterStepTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_counterService is null || CounterStepTextBox is null)
        {
            return;
        }

        _counterService.SetStep(GetCounterStep());
        UpdateCounterButtons();
        UpdateStepWarnings();
        SaveSettings();
    }

    private void StepMinutesTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (StepMinutesTextBox is null)
        {
            return;
        }

        UpdateStepWarnings();
    }

    private void UpdateCounterButtons()
    {
        var step = _counterService.Step;
        IncreaseCounterButton.Content = $"+{step}";
        DecreaseCounterButton.Content = $"-{step}";
    }

    private void UpdateStepWarnings()
    {
        if (StepMinutesWarningTextBlock is null || CounterStepWarningTextBlock is null)
        {
            return;
        }

        StepMinutesWarningTextBlock.Visibility = IsInRange(StepMinutesTextBox.Text, MinStepMinutes, MaxStepMinutes)
            ? Visibility.Collapsed
            : Visibility.Visible;
        CounterStepWarningTextBlock.Visibility = IsInRange(CounterStepTextBox.Text, MinCounterStep, MaxCounterStep)
            ? Visibility.Collapsed
            : Visibility.Visible;
    }

    private static bool IsInRange(string text, int min, int max)
    {
        return int.TryParse(text, out var value) && value >= min && value <= max;
    }

    private static int Clamp(int value, int min, int max)
    {
        if (value < min)
        {
            return min;
        }

        return value > max ? max : value;
    }

    private void EnqueueMinutesDelta(int minutes)
    {
        Interlocked.Add(ref _pendingMinutesDelta, minutes);
    }

    private void ApplyPendingMinutes()
    {
        var delta = Interlocked.Exchange(ref _pendingMinutesDelta, 0);
        if (delta == 0)
        {
            return;
        }

        if (delta > int.MaxValue)
        {
            delta = int.MaxValue;
        }
        else if (delta < int.MinValue)
        {
            delta = int.MinValue;
        }

        _timerService.AdjustMinutes((int)delta);
    }
}
