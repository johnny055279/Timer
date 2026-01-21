using System;
using System.Windows;

namespace Timer;

public partial class TwitchWindow
{
    private async void TwitchStartPoll_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(TwitchPollTitleTextBox.Text))
        {
            MessageBox.Show(this, "Enter a poll title. (請輸入投票標題)", "Twitch");
            return;
        }

        if (!TryGetMinutes(TwitchPollMinutesTextBox.Text, out var minutes))
        {
            MessageBox.Show(this, $"Enter {MinMinutes}-{MaxMinutes} minutes. (請輸入 {MinMinutes}-{MaxMinutes} 分鐘)", "Twitch");
            return;
        }

        var isAdd = IsAddAction(TwitchPollActionComboBox.SelectedItem as string);
        var delta = isAdd ? minutes : -minutes;
        _pollDecisionService.SetPendingDelta(delta);

        try
        {
            await _twitchClient.StartPollAsync(TwitchPollTitleTextBox.Text.Trim(), 60);
            TwitchPollStatusTextBlock.Text = $"Poll started: {FormatPollDelta(delta)} mins (投票已開始)";
        }
        catch (Exception ex)
        {
            _pollDecisionService.Clear();
            _logService.LogError("Start poll failed.", ex);
            MessageBox.Show(this, ex.Message, "Twitch");
        }
    }

    private static bool IsAddAction(string? value)
    {
        return value is not null && value.StartsWith("Add", StringComparison.OrdinalIgnoreCase);
    }

    private static string FormatPollDelta(int minutes)
    {
        return minutes >= 0 ? $"+{minutes}" : minutes.ToString();
    }
}
