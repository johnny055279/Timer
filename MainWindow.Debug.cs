using System;
using System.Windows;
using System.Windows.Input;

namespace Timer;

public partial class MainWindow
{
    private void OnLogAppended(object? sender, string logLine)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.Invoke(() => _debugWindow?.AppendLog(logLine));
            return;
        }

        _debugWindow?.AppendLog(logLine);
    }

    private void HelpButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new HelpDialog { Owner = this };
        dialog.ShowDialog();
    }

    private void DebugButton_Click(object sender, RoutedEventArgs e)
    {
        if (_debugWindow is null || !_debugWindow.IsVisible)
        {
            _debugWindow = new DebugLogWindow { Owner = this };
            _debugWindow.SetLog(_logService.GetLog());
            _debugWindow.Closed += (_, _) => _debugWindow = null;
            _debugWindow.Show();
            return;
        }

        _debugWindow.Activate();
    }

    private void TwitchButton_Click(object sender, RoutedEventArgs e)
    {
        if (_twitchWindow is null || !_twitchWindow.IsVisible)
        {
            _twitchWindow = new TwitchWindow(
                _twitchClient,
                _rewardMappingService,
                _bitsMappingService,
                _pollDecisionService,
                _logService,
                _settingsStore)
            {
                Owner = this
            };
            _twitchWindow.Closed += (_, _) =>
            {
                _twitchWindow = null;
                if (WindowState == WindowState.Minimized)
                {
                    WindowState = WindowState.Normal;
                }

                if (!IsVisible)
                {
                    Show();
                }

                Activate();
            };
            _twitchWindow.Show();
            return;
        }

        _twitchWindow.Activate();
    }

    private void OnWindowClosed(object? sender, EventArgs e)
    {
        InputManager.Current.PreProcessInput -= OnPreProcessInput;
        Closed -= OnWindowClosed;
        Loaded -= OnWindowLoaded;
        _logService.LogAppended -= OnLogAppended;
        _twitchClient.StatusChanged -= OnTwitchStatusChanged;
        _ = _twitchClient.DisconnectAsync();
    }
}
