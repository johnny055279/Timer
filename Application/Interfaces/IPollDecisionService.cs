namespace Timer.Application.Interfaces;

public interface IPollDecisionService
{
    int PendingDelta { get; }
    void SetPendingDelta(int deltaMinutes);
    int GetDeltaForWinner(string winnerTitle);
    void Clear();
}
