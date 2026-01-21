using System;
using System.Windows;
using Timer.Infrastructure.Security;

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

    private void TwitchReset_Click(object sender, RoutedEventArgs e)
    {
        var store = new WindowsCredentialStore();
        store.Delete(TwitchTokenKey);
        TwitchStatusTextBlock.Text = "Not connected (未連線)";
        TwitchUserCodeTextBlock.Text = string.Empty;
        TwitchVerifyUrlTextBlock.Text = string.Empty;
        TwitchConnectPanel.Visibility = Visibility.Visible;
    }

    private void TwitchCopyCode_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(TwitchUserCodeTextBlock.Text))
        {
            Clipboard.SetText(TwitchUserCodeTextBlock.Text);
        }
    }
}
