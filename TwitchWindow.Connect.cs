using System;
using System.Windows;

namespace Timer;

public partial class TwitchWindow
{
    private async void TwitchConnectButton_Click(object sender, RoutedEventArgs e)
    {
        TwitchConnectButton.IsEnabled = false;
        TwitchStatusTextBlock.Text = "Connecting... (連線中)";
        try
        {
            await _twitchClient.ConnectAsync();
        }
        catch (Exception ex)
        {
            TwitchStatusTextBlock.Text = "Connection error (連線失敗)";
            _logService.LogError("Twitch connect failed.", ex);
            MessageBox.Show(this, ex.Message, "Twitch");
        }
        finally
        {
            TwitchConnectButton.IsEnabled = true;
        }
    }

    private async void TwitchReset_Click(object sender, RoutedEventArgs e)
    {
        await _twitchClient.RevokeAsync();
        TwitchStatusTextBlock.Text = "Not connected (未連線)";
        TwitchUserCodeTextBlock.Text = string.Empty;
        TwitchVerifyUrlTextBlock.Text = string.Empty;
        TwitchConnectPanel.Visibility = Visibility.Visible;
        TwitchConnectButton.Visibility = Visibility.Visible;
        TwitchResetButton.Visibility = Visibility.Visible;
        TwitchVerificationCodePanel.Visibility = Visibility.Collapsed;
        TwitchVerificationUrlPanel.Visibility = Visibility.Collapsed;
        _twitchClient.NotifyStatus("Not connected");
    }

    private void TwitchCopyCode_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(TwitchUserCodeTextBlock.Text))
        {
            Clipboard.SetText(TwitchUserCodeTextBlock.Text);
        }
    }
}
