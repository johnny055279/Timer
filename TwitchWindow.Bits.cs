using System;
using System.Linq;
using System.Windows;
using Timer.Application.Models;
using Timer.Domain.Entities;

namespace Timer;

public partial class TwitchWindow
{
    private void DebugTriggerBitsButton_Click(object sender, RoutedEventArgs e)
    {
        if (TwitchBitsMappingsListBox.SelectedItem is not TwitchBitsMapping mapping)
        {
            MessageBox.Show(this, "Select a mapping first. (請先選擇對應)", "Twitch");
            return;
        }

        _twitchClient.SimulateBitsCheered(mapping.Bits);
    }

    private void TwitchBitsTargetComboBox_SelectionChanged(object sender, RoutedEventArgs e)
    {
        UpdateBitsActionOptions();
    }

    private void TwitchAddBitsMapping_Click(object sender, RoutedEventArgs e)
    {
        if (!TryGetBitsAmount(TwitchBitsAmountTextBox.Text, out var bits))
        {
            MessageBox.Show(this, "Enter bits amount (>= 1). (請輸入 1 以上的小奇點數)", "Twitch");
            return;
        }

        var target = GetBitsTarget();
        var action = GetBitsAction();
        var amount = 0;
        if (target == TwitchRewardTarget.Countdown)
        {
            if (!TryGetMinutes(TwitchBitsValueTextBox.Text, out amount))
            {
                MessageBox.Show(this, $"Enter {MinMinutes}-{MaxMinutes} minutes. (請輸入 {MinMinutes}-{MaxMinutes} 分鐘)", "Twitch");
                return;
            }

            if (action == TwitchRewardAction.Subtract)
            {
                amount = -amount;
            }
        }
        else
        {
            if (action != TwitchRewardAction.Reset)
            {
                if (!TryGetCounterAmount(TwitchBitsValueTextBox.Text, out amount))
                {
                    MessageBox.Show(this, "Enter 1 or greater. (請輸入 1 以上)", "Twitch");
                    return;
                }
            }
        }

        _bitsMappingService.AddOrUpdateMapping(bits, target, action, amount);
        RefreshBitsMappings();
        SaveBitsMappings();
    }

    private void TwitchRemoveBitsMapping_Click(object sender, RoutedEventArgs e)
    {
        if (TwitchBitsMappingsListBox.SelectedItem is not TwitchBitsMapping mapping)
        {
            MessageBox.Show(this, "Select a mapping to remove. (請選擇要移除的對應)", "Twitch");
            return;
        }

        _bitsMappingService.RemoveMapping(mapping);
        RefreshBitsMappings();
        SaveBitsMappings();
    }

    private void TwitchBitsMappingsListBox_SelectionChanged(object sender, RoutedEventArgs e)
    {
        if (TwitchBitsMappingsListBox.SelectedItem is TwitchBitsMapping mapping)
        {
            DebugSelectedBitsTextBlock.Text = mapping.Display;
            return;
        }

        DebugSelectedBitsTextBlock.Text = "None";
    }

    private void DebugSimulateBitsButton_Click(object sender, RoutedEventArgs e)
    {
        if (!TryGetBitsAmount(TwitchBitsAmountTextBox.Text, out var bits))
        {
            MessageBox.Show(this, "Enter bits amount (>= 1). (請輸入 1 以上的小奇點數)", "Twitch");
            return;
        }

        _twitchClient.SimulateBitsCheered(bits);
    }

    private void RefreshBitsMappings()
    {
        _twitchBitsMappings.Clear();
        foreach (var mapping in _bitsMappingService.Mappings)
        {
            _twitchBitsMappings.Add(mapping);
        }
    }

    private void LoadBitsMappingsFromSettings(bool useDebug)
    {
        var settings = _settingsStore.Load();
        if (useDebug)
        {
            _bitsMappingService.ReplaceMappings(Array.Empty<TwitchBitsMapping>());
            RefreshBitsMappings();
            return;
        }

        var sourceMappings = settings.BitsMappings;
        if (sourceMappings is null || sourceMappings.Count == 0)
        {
            _bitsMappingService.ReplaceMappings(Array.Empty<TwitchBitsMapping>());
            RefreshBitsMappings();
            return;
        }

        var mappings = sourceMappings
            .Where(item => item.Bits > 0)
            .Select(item => new TwitchBitsMapping(
                item.Bits,
                Enum.TryParse(item.Target, true, out TwitchRewardTarget target)
                    ? target
                    : TwitchRewardTarget.Countdown,
                Enum.TryParse(item.Action, true, out TwitchRewardAction action)
                    ? action
                    : TwitchRewardAction.Add,
                item.Amount))
            .ToList();

        _bitsMappingService.ReplaceMappings(mappings);
        RefreshBitsMappings();
    }

    private void ClearDebugBitsMappings()
    {
        if (_bitsMappingService.Mappings.Count == 0)
        {
            return;
        }

        _bitsMappingService.ReplaceMappings(Array.Empty<TwitchBitsMapping>());
        RefreshBitsMappings();
    }

    private void SaveBitsMappings()
    {
        var settings = _settingsStore.Load();
        if (settings.UseDebugEventSub)
        {
            return;
        }

        var savedMappings = _bitsMappingService.Mappings
            .Select(mapping => new TwitchBitsMappingSetting(
                mapping.Bits,
                mapping.Target.ToString(),
                mapping.Action.ToString(),
                mapping.Amount))
            .ToList();

        settings.BitsMappings = savedMappings;
        _settingsStore.Save(settings);
    }

    private void UpdateBitsActionOptions()
    {
        if (TwitchBitsActionComboBox is null || TwitchBitsValueTextBox is null)
        {
            return;
        }

        var target = GetBitsTarget();
        if (target == TwitchRewardTarget.Counter)
        {
            TwitchBitsActionComboBox.ItemsSource = RewardCounterActions;
            TwitchBitsActionComboBox.SelectedIndex = 0;
            TwitchBitsValueTextBox.IsEnabled = true;
            return;
        }

        TwitchBitsActionComboBox.ItemsSource = RewardCountdownActions;
        TwitchBitsActionComboBox.SelectedIndex = 0;
        TwitchBitsValueTextBox.IsEnabled = true;
    }

    private TwitchRewardTarget GetBitsTarget()
    {
        var value = TwitchBitsTargetComboBox.SelectedItem as string;
        return value is not null && value.StartsWith("Counter", StringComparison.OrdinalIgnoreCase)
            ? TwitchRewardTarget.Counter
            : TwitchRewardTarget.Countdown;
    }

    private TwitchRewardAction GetBitsAction()
    {
        var value = TwitchBitsActionComboBox.SelectedItem as string;
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
}
