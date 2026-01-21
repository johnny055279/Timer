using System;
using System.Diagnostics;
using System.Windows;

namespace Timer;

public partial class TwitchWindow
{
    private void OnStatusChanged(object? sender, string status)
    {
        Dispatcher.Invoke(() =>
        {
            TwitchStatusTextBlock.Text = FormatStatus(status);
            if (status.StartsWith("Connected", StringComparison.OrdinalIgnoreCase))
            {
                TwitchConnectPanel.Visibility = Visibility.Collapsed;
            }
            else if (status.Contains("expired", StringComparison.OrdinalIgnoreCase)
                     || status.Contains("failed", StringComparison.OrdinalIgnoreCase))
            {
                TwitchConnectPanel.Visibility = Visibility.Visible;
            }
        });
    }

    private void OnDeviceCodeReceived(object? sender, (string UserCode, string VerifyUrl) data)
    {
        Dispatcher.Invoke(() =>
        {
            TwitchUserCodeTextBlock.Text = data.UserCode;
            TwitchVerifyUrlTextBlock.Text = data.VerifyUrl;
            Process.Start(new ProcessStartInfo(data.VerifyUrl) { UseShellExecute = true });
        });
    }

    private void OnWindowClosed(object? sender, EventArgs e)
    {
        _twitchClient.StatusChanged -= OnStatusChanged;
        _twitchClient.DeviceCodeReceived -= OnDeviceCodeReceived;
        Closed -= OnWindowClosed;
    }

    private static string FormatStatus(string status)
    {
        if (status.StartsWith("Connected as ", StringComparison.OrdinalIgnoreCase))
        {
            return $"{status} (已連線)";
        }

        if (string.Equals(status, "Connected", StringComparison.OrdinalIgnoreCase))
        {
            return "Connected (已連線)";
        }

        if (string.Equals(status, "Complete verification", StringComparison.OrdinalIgnoreCase))
        {
            return "Complete verification (完成驗證)";
        }

        if (status.Contains("expired", StringComparison.OrdinalIgnoreCase))
        {
            return $"{status} (授權過期)";
        }

        if (status.Contains("failed", StringComparison.OrdinalIgnoreCase))
        {
            return $"{status} (連線失敗)";
        }

        if (status.Contains("rate limit", StringComparison.OrdinalIgnoreCase))
        {
            return $"{status} (已達限制)";
        }

        return status;
    }
}
