using Timer.Application.Interfaces;

namespace Timer.Application.Services;

public sealed class PollDecisionService : IPollDecisionService
{
    private int _pendingDelta;

    public int PendingDelta => _pendingDelta;

    public void SetPendingDelta(int deltaMinutes)
    {
        _pendingDelta = deltaMinutes;
    }

    public int GetDeltaForWinner(string winnerTitle)
    {
        if (string.Equals(winnerTitle, "Agree", StringComparison.OrdinalIgnoreCase))
        {
            return _pendingDelta;
        }

        return 0;
    }

    public void Clear()
    {
        _pendingDelta = 0;
    }
}
