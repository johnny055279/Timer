using System;
using System.Threading.Tasks;
using System.Windows;

namespace Timer;

public partial class MainWindow
{
    private void OnTwitchStatusChanged(object? sender, string status)
    {
        Dispatcher.Invoke(() =>
        {
#if DEBUG
            if (_settingsStore.Load().UseDebugEventSub)
            {
                UpdateTwitchConnectedIndicator(false);
                ShowTwitchTokenWarning(false);
                return;
            }
#endif
            var connected = status.StartsWith("Connected", StringComparison.OrdinalIgnoreCase)
                || (status.StartsWith("Twitch connected", StringComparison.OrdinalIgnoreCase)
                    && !status.Contains("disconnected", StringComparison.OrdinalIgnoreCase));
            UpdateTwitchConnectedIndicator(connected);
            if (connected)
            {
                ShowTwitchTokenWarning(false);
                return;
            }

            if (status.Contains("Not connected", StringComparison.OrdinalIgnoreCase))
            {
                ShowTwitchTokenWarning(true);
                return;
            }

            if (status.Contains("expired", StringComparison.OrdinalIgnoreCase)
                || status.Contains("failed", StringComparison.OrdinalIgnoreCase)
                || status.Contains("permission denied", StringComparison.OrdinalIgnoreCase)
                || status.Contains("authorization", StringComparison.OrdinalIgnoreCase))
            {
                ShowTwitchTokenWarning(true);
            }
        });
    }

    private async Task TryAutoConnectTwitchAsync()
    {
        if (_twitchAutoConnectAttempted)
        {
            return;
        }

        _twitchAutoConnectAttempted = true;
#if DEBUG
        if (_settingsStore.Load().UseDebugEventSub)
        {
            UpdateTwitchConnectedIndicator(false);
            ShowTwitchTokenWarning(false);
            return;
        }
#endif
        ShowTwitchTokenWarning(true);
        var connected = await _twitchClient.TryReconnectAsync();
        UpdateTwitchConnectedIndicator(connected);
        ShowTwitchTokenWarning(!connected);
    }

    private void UpdateTwitchConnectedIndicator(bool isConnected)
    {
        if (TwitchConnectedBadge is null)
        {
            return;
        }

        TwitchConnectedBadge.Visibility = isConnected ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ShowTwitchTokenWarning(bool show)
    {
        if (TwitchTokenWarningBorder is null)
        {
            return;
        }

        TwitchTokenWarningBorder.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ApplyBitsAdjustment(int bits)
    {
        var mapping = _bitsMappingService.TryGetMapping(bits);
        if (mapping is null)
        {
            return;
        }

        if (mapping.Target == Timer.Domain.Entities.TwitchRewardTarget.Countdown)
        {
            EnqueueMinutesDelta(mapping.Amount);
            return;
        }

        if (mapping.Target == Timer.Domain.Entities.TwitchRewardTarget.Counter)
        {
            switch (mapping.Action)
            {
                case Timer.Domain.Entities.TwitchRewardAction.Add:
                    _counterService.IncreaseBy(mapping.Amount);
                    break;
                case Timer.Domain.Entities.TwitchRewardAction.Subtract:
                    _counterService.DecreaseBy(mapping.Amount);
                    break;
                case Timer.Domain.Entities.TwitchRewardAction.Reset:
                    _counterService.Reset();
                    break;
            }

            UpdateCounterDisplay();
        }
    }

    private void ApplyRewardAdjustment(string rewardId)
    {
        var mapping = _rewardMappingService.TryGetMapping(rewardId);
        if (mapping is null)
        {
            return;
        }

        if (mapping.Target == Timer.Domain.Entities.TwitchRewardTarget.Countdown)
        {
            EnqueueMinutesDelta(mapping.Minutes);
            return;
        }

        if (mapping.Target == Timer.Domain.Entities.TwitchRewardTarget.Counter)
        {
            switch (mapping.Action)
            {
                case Timer.Domain.Entities.TwitchRewardAction.Add:
                    _counterService.IncreaseBy(mapping.Minutes);
                    break;
                case Timer.Domain.Entities.TwitchRewardAction.Subtract:
                    _counterService.DecreaseBy(mapping.Minutes);
                    break;
                case Timer.Domain.Entities.TwitchRewardAction.Reset:
                    _counterService.Reset();
                    break;
            }

            UpdateCounterDisplay();
        }
    }

    private void ApplyPollAdjustment(string winnerTitle)
    {
        if (_pollDecisionService.PendingDelta == 0)
        {
            return;
        }

        var delta = _pollDecisionService.GetDeltaForWinner(winnerTitle);
        if (delta != 0)
        {
            EnqueueMinutesDelta(delta);
        }

        _pollDecisionService.Clear();
    }
}
