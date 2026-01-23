using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Timer.Application.Models;
using Timer.Domain.Entities;

namespace Timer;

public partial class TwitchWindow
{
    private static readonly TwitchReward[] DebugRewards =
    [
        new TwitchReward("debug-reward-1", "Debug Reward 1", 100),
        new TwitchReward("debug-reward-2", "Debug Reward 2", 500),
        new TwitchReward("debug-reward-3", "Debug Reward 3", 1000)
    ];

    private void DebugTriggerRewardButton_Click(object sender, RoutedEventArgs e)
    {
        if (TwitchRewardMappingsListBox.SelectedItem is not TwitchRewardMapping mapping)
        {
            MessageBox.Show(this, "Select a mapping first. (請先選擇對應)", "Twitch");
            return;
        }

        _twitchClient.SimulateRewardRedeemed(mapping.RewardId);
    }

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
#if DEBUG
            if (_settingsStore.Load().UseDebugEventSub)
            {
                LoadDebugRewards();
                return;
            }
#endif

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

    private void LoadDebugRewards()
    {
        _twitchRewards.Clear();
        foreach (var reward in DebugRewards)
        {
            _twitchRewards.Add(reward);
        }

        if (_twitchRewards.Count > 0)
        {
            TwitchRewardsComboBox.SelectedIndex = 0;
        }
    }

    private void TwitchRewardTargetComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateRewardActionOptions();
    }

    private void ClearDebugRewards()
    {
        _twitchRewards.Clear();
        TwitchRewardsComboBox.SelectedIndex = -1;
        TwitchRewardIdTextBox.Text = string.Empty;
    }

    private void TwitchAddRewardMapping_Click(object sender, RoutedEventArgs e)
    {
        var reward = TwitchRewardsComboBox.SelectedItem as TwitchReward;
#if DEBUG
        if (reward is null && _settingsStore.Load().UseDebugEventSub)
        {
            LoadDebugRewards();
            reward = TwitchRewardsComboBox.SelectedItem as TwitchReward;
        }
#endif
        if (reward is null)
        {
            MessageBox.Show(this, "Select a reward first. (請先選擇獎勵)", "Twitch");
            return;
        }

        var target = GetRewardTarget();
        var action = GetRewardAction();
        var minutes = 0;
        if (target == TwitchRewardTarget.Countdown)
        {
            if (!TryGetMinutes(TwitchRewardMinutesTextBox.Text, out minutes))
            {
                MessageBox.Show(this, $"Enter {MinMinutes}-{MaxMinutes} minutes. (請輸入 {MinMinutes}-{MaxMinutes} 分鐘)", "Twitch");
                return;
            }

            if (action == TwitchRewardAction.Subtract)
            {
                minutes = -minutes;
            }
        }
        else
        {
            if (action != TwitchRewardAction.Reset)
            {
                if (!TryGetCounterAmount(TwitchRewardMinutesTextBox.Text, out minutes))
                {
                    MessageBox.Show(this, "Enter 1 or greater. (請輸入 1 以上)", "Twitch");
                    return;
                }
            }
        }

        _rewardMappingService.AddOrUpdateMapping(reward, target, action, minutes);
        RefreshRewardMappings();
        SaveRewardMappings();
    }

    private void DebugSimulateRewardButton_Click(object sender, RoutedEventArgs e)
    {
        if (TwitchRewardsComboBox.SelectedItem is not TwitchReward reward)
        {
            MessageBox.Show(this, "Select a reward first. (請先選擇獎勵)", "Twitch");
            return;
        }

        _twitchClient.SimulateRewardRedeemed(reward.Id);
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
        SaveRewardMappings();
    }

    private void TwitchRewardMappingsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (TwitchRewardMappingsListBox.SelectedItem is TwitchRewardMapping mapping)
        {
            DebugSelectedRewardTextBlock.Text = mapping.Display;
            return;
        }

        DebugSelectedRewardTextBlock.Text = "None";
    }

    private void RefreshRewardMappings()
    {
        _twitchRewardMappings.Clear();
        foreach (var mapping in _rewardMappingService.Mappings)
        {
            _twitchRewardMappings.Add(mapping);
        }
    }

    private void UpdateRewardActionOptions()
    {
        if (TwitchRewardActionComboBox is null || TwitchRewardMinutesTextBox is null)
        {
            return;
        }

        var target = GetRewardTarget();
        if (target == TwitchRewardTarget.Counter)
        {
            TwitchRewardActionComboBox.ItemsSource = RewardCounterActions;
            TwitchRewardActionComboBox.SelectedIndex = 0;
            TwitchRewardMinutesTextBox.IsEnabled = true;
            return;
        }

        TwitchRewardActionComboBox.ItemsSource = RewardCountdownActions;
        TwitchRewardActionComboBox.SelectedIndex = 0;
        TwitchRewardMinutesTextBox.IsEnabled = true;
    }

    private TwitchRewardTarget GetRewardTarget()
    {
        var value = TwitchRewardTargetComboBox.SelectedItem as string;
        return value is not null && value.StartsWith("Counter", StringComparison.OrdinalIgnoreCase)
            ? TwitchRewardTarget.Counter
            : TwitchRewardTarget.Countdown;
    }

    private TwitchRewardAction GetRewardAction()
    {
        var value = TwitchRewardActionComboBox.SelectedItem as string;
        if (value is null)
        {
            return TwitchRewardAction.Add;
        }

        if (value.StartsWith("Subtract", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("Decrease", StringComparison.OrdinalIgnoreCase))
        {
            return TwitchRewardAction.Subtract;
        }

        if (value.StartsWith("Reset", StringComparison.OrdinalIgnoreCase))
        {
            return TwitchRewardAction.Reset;
        }

        return TwitchRewardAction.Add;
    }

    private void ClearDebugMappings()
    {
        var toRemove = _rewardMappingService.Mappings
            .Where(mapping => mapping.RewardId.StartsWith("debug-", StringComparison.OrdinalIgnoreCase))
            .ToList();

        foreach (var mapping in toRemove)
        {
            _rewardMappingService.RemoveMapping(mapping);
        }

        RefreshRewardMappings();
    }

    private void LoadRewardMappingsFromSettings(bool useDebug)
    {
        var settings = _settingsStore.Load();
        if (useDebug)
        {
            _rewardMappingService.ReplaceMappings(Array.Empty<TwitchRewardMapping>());
            RefreshRewardMappings();
            return;
        }

        var sourceMappings = settings.RewardMappings;
        if (sourceMappings is null || sourceMappings.Count == 0)
        {
            _rewardMappingService.ReplaceMappings(Array.Empty<TwitchRewardMapping>());
            RefreshRewardMappings();
            return;
        }

        var mappings = sourceMappings
            .Where(item => !string.IsNullOrWhiteSpace(item.RewardId))
            .Select(item => new TwitchRewardMapping(
                item.RewardId,
                item.Title ?? string.Empty,
                Enum.TryParse(item.Target, true, out TwitchRewardTarget target)
                    ? target
                    : TwitchRewardTarget.Countdown,
                Enum.TryParse(item.Action, true, out TwitchRewardAction action)
                    ? action
                    : TwitchRewardAction.Add,
                item.Amount))
            .ToList();

        _rewardMappingService.ReplaceMappings(mappings);
        RefreshRewardMappings();
    }

    private void SaveRewardMappings()
    {
        var settings = _settingsStore.Load();
        if (settings.UseDebugEventSub)
        {
            return;
        }

        var savedMappings = _rewardMappingService.Mappings
            .Select(mapping => new TwitchRewardMappingSetting(
                mapping.RewardId,
                mapping.Title,
                mapping.Target.ToString(),
                mapping.Action.ToString(),
                mapping.Minutes))
            .ToList();
        settings.RewardMappings = savedMappings;
        _settingsStore.Save(settings);
    }
}
