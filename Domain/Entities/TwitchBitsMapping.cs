namespace Timer.Domain.Entities;

public sealed record TwitchBitsMapping(
    int Bits,
    TwitchRewardTarget Target,
    TwitchRewardAction Action,
    int Amount)
{
    public string Display => Target switch
    {
        TwitchRewardTarget.Countdown => $"Bits {Bits}: Countdown {Amount:+#;-#;0} mins",
        TwitchRewardTarget.Counter => $"Bits {Bits}: Counter {FormatCounterAction(Action, Amount)}",
        _ => $"Bits {Bits}"
    };

    private static string FormatCounterAction(TwitchRewardAction action, int amount)
    {
        return action switch
        {
            TwitchRewardAction.Add => $"+{System.Math.Abs(amount)}",
            TwitchRewardAction.Subtract => $"-{System.Math.Abs(amount)}",
            TwitchRewardAction.Reset => "Reset",
            _ => "-"
        };
    }
}
