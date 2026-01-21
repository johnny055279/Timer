using System;
using System.Windows;
using System.Windows.Controls;
using Timer.Domain.Entities;

namespace Timer;

public partial class TwitchWindow
{
    private void TwitchRewardsComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (TwitchRewardsComboBox.SelectedItem is TwitchReward reward)
        {
            TwitchRewardIdTextBox.Text = reward.Id;
        }
        else
        {
            TwitchRewardIdTextBox.Text = string.Empty;
        }
    }

    private void TwitchCopyRewardId_Click(object sender, RoutedEventArgs e)
    {
        if (TwitchRewardsComboBox.SelectedItem is TwitchReward reward)
        {
            Clipboard.SetText(reward.Id);
        }
    }

    private async void TwitchLoadRewards_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var rewards = await _twitchClient.LoadRewardsAsync();
            _twitchRewards.Clear();
            foreach (var reward in rewards)
            {
                _twitchRewards.Add(reward);
            }

            if (_twitchRewards.Count > 0)
            {
                TwitchRewardsComboBox.SelectedIndex = 0;
            }
            else
            {
                MessageBox.Show(this, "No rewards found for this channel. (沒有可用獎勵)", "Twitch");
            }
        }
        catch (Exception ex)
        {
            _logService.LogError("Load rewards failed.", ex);
            MessageBox.Show(this, ex.Message, "Twitch");
        }
    }

    private void TwitchAddRewardMapping_Click(object sender, RoutedEventArgs e)
    {
        if (TwitchRewardsComboBox.SelectedItem is not TwitchReward reward)
        {
            MessageBox.Show(this, "Select a reward first. (請先選擇獎勵)", "Twitch");
            return;
        }

        if (!TryGetMinutes(TwitchRewardMinutesTextBox.Text, out var minutes))
        {
            MessageBox.Show(this, $"Enter {MinMinutes}-{MaxMinutes} minutes. (請輸入 {MinMinutes}-{MaxMinutes} 分鐘)", "Twitch");
            return;
        }

        var isAdd = IsAddAction(TwitchRewardActionComboBox.SelectedItem as string);
        var delta = isAdd ? minutes : -minutes;
        _rewardMappingService.AddOrUpdateMapping(reward, delta);
        RefreshRewardMappings();
    }

    private void TwitchRemoveRewardMapping_Click(object sender, RoutedEventArgs e)
    {
        if (TwitchRewardMappingsListBox.SelectedItem is not TwitchRewardMapping mapping)
        {
            MessageBox.Show(this, "Select a mapping to remove. (請選擇要移除的對應)", "Twitch");
            return;
        }

        _rewardMappingService.RemoveMapping(mapping);
        RefreshRewardMappings();
    }

    private void RefreshRewardMappings()
    {
        _twitchRewardMappings.Clear();
        foreach (var mapping in _rewardMappingService.Mappings)
        {
            _twitchRewardMappings.Add(mapping);
        }
    }
}
