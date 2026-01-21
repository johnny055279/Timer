namespace Timer.Domain.Entities;

public sealed record TwitchRewardMapping(string RewardId, string Title, int Minutes)
{
    public string Display => $"{Title}: {Minutes:+#;-#;0} mins";
}
