using System.Windows;

namespace Timer;

public partial class MainWindow
{
    private void ApplyRewardAdjustment(string rewardId)
    {
        var delta = _rewardMappingService.TryGetDelta(rewardId);
        if (delta is null)
        {
            return;
        }

        EnqueueMinutesDelta(delta.Value);
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
