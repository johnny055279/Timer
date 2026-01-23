namespace Timer.Domain.Entities;

public enum TwitchRewardTarget
{
    Countdown,
    Counter
}

public enum TwitchRewardAction
{
    Add,
    Subtract,
    Reset
}

public sealed record TwitchRewardMapping(
    string RewardId,
    string Title,
    TwitchRewardTarget Target,
    TwitchRewardAction Action,
    int Minutes)
{
    public string Display => Target switch
    {
        TwitchRewardTarget.Countdown => $"{Title}: Countdown {Minutes:+#;-#;0} mins",
        TwitchRewardTarget.Counter => $"{Title}: Counter {FormatCounterAction(Action, Minutes)}",
        _ => Title
    };

    private static string FormatCounterAction(TwitchRewardAction action, int amount)
    {
        return action switch
        {
            TwitchRewardAction.Add => $"+{Math.Abs(amount)}",
            TwitchRewardAction.Subtract => $"-{Math.Abs(amount)}",
            TwitchRewardAction.Reset => "Reset",
            _ => "-"
        };
    }
}
