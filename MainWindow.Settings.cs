using System;
using System.Windows.Controls;
using Timer.Application.Models;

namespace Timer;

public partial class MainWindow
{
    private void LoadSettings()
    {
        _isLoadingSettings = true;
        var settings = _settingsStore.Load();
        if (!string.IsNullOrWhiteSpace(settings.CountdownTitle))
        {
            _isLoadingTitle = true;
            CountdownTitleTextBox.Text = settings.CountdownTitle;
            _isLoadingTitle = false;
        }

        if (settings.CounterStep > 0)
        {
            var clampedStep = Clamp(settings.CounterStep, MinCounterStep, MaxCounterStep);
            CounterStepTextBox.Text = clampedStep.ToString();
            _counterService.SetStep(clampedStep);
        }

        if (settings.CounterValue > 0)
        {
            _counterService.SetCount(settings.CounterValue);
        }

        UpdateCounterDisplay();
        UpdateCounterButtons();
        UpdateStepWarnings();
        _isLoadingSettings = false;
    }

    private void SaveSettings()
    {
        if (_isLoadingSettings || _settingsStore is null || CountdownTitleTextBox is null)
        {
            return;
        }

        var settings = new AppSettings
        {
            CountdownTitle = CountdownTitleTextBox.Text?.Trim() ?? string.Empty,
            CounterValue = _counterService.Count,
            CounterStep = _counterService.Step
        };
        _settingsStore.Save(settings);
    }

    private void CountdownTitleTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isLoadingTitle)
        {
            return;
        }

        SaveSettings();
    }
}
