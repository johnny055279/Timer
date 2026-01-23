using System;
using System.Collections.Generic;
using System.Windows.Controls;
using Timer.Application.Models;
using Timer.Domain.Entities;

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

        if (!string.IsNullOrWhiteSpace(settings.CounterTitle))
        {
            _isLoadingCounterTitle = true;
            CounterTitleTextBox.Text = settings.CounterTitle;
            _isLoadingCounterTitle = false;
        }

        LoadRewardMappings(settings);
        LoadBitsMappings(settings);
    }

    private void SaveSettings()
    {
        if (_settingsStore is null || CountdownTitleTextBox is null || CounterTitleTextBox is null)
        {
            return;
        }

        var settings = _settingsStore.Load();
        settings.CountdownTitle = CountdownTitleTextBox.Text?.Trim() ?? string.Empty;
        settings.CounterTitle = CounterTitleTextBox.Text?.Trim() ?? string.Empty;
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

    private void CounterTitleTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isLoadingCounterTitle)
        {
            return;
        }

        SaveSettings();
    }

    private void LoadRewardMappings(AppSettings settings)
    {
        var sourceMappings = settings.RewardMappings;
        if (sourceMappings is null || sourceMappings.Count == 0)
        {
            return;
        }

        var mappings = new List<TwitchRewardMapping>();
        foreach (var item in sourceMappings)
        {
            if (string.IsNullOrWhiteSpace(item.RewardId))
            {
                continue;
            }

            if (!Enum.TryParse(item.Target, true, out TwitchRewardTarget target))
            {
                target = TwitchRewardTarget.Countdown;
            }

            if (!Enum.TryParse(item.Action, true, out TwitchRewardAction action))
            {
                action = TwitchRewardAction.Add;
            }

            mappings.Add(new TwitchRewardMapping(
                item.RewardId,
                item.Title ?? string.Empty,
                target,
                action,
                item.Amount));
        }

        _rewardMappingService.ReplaceMappings(mappings);
    }

    private void LoadBitsMappings(AppSettings settings)
    {
        var sourceMappings = settings.BitsMappings;
        if (sourceMappings is null || sourceMappings.Count == 0)
        {
            return;
        }

        var mappings = new List<TwitchBitsMapping>();
        foreach (var item in sourceMappings)
        {
            if (item.Bits <= 0)
            {
                continue;
            }

            if (!Enum.TryParse(item.Target, true, out TwitchRewardTarget target))
            {
                target = TwitchRewardTarget.Countdown;
            }

            if (!Enum.TryParse(item.Action, true, out TwitchRewardAction action))
            {
                action = TwitchRewardAction.Add;
            }

            mappings.Add(new TwitchBitsMapping(
                item.Bits,
                target,
                action,
                item.Amount));
        }

        _bitsMappingService.ReplaceMappings(mappings);
    }
}
