using System;
using System.Windows.Controls;
using Timer.Application.Models;

namespace Timer;

public partial class MainWindow
{
    private void LoadSettings()
    {
        var settings = _settingsStore.Load();
        if (!string.IsNullOrWhiteSpace(settings.CountdownTitle))
        {
            _isLoadingTitle = true;
            CountdownTitleTextBox.Text = settings.CountdownTitle;
            _isLoadingTitle = false;
        }
    }

    private void SaveSettings()
    {
        if (_settingsStore is null || CountdownTitleTextBox is null)
        {
            return;
        }

        var settings = new AppSettings
        {
            CountdownTitle = CountdownTitleTextBox.Text?.Trim() ?? string.Empty
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
