namespace Timer.Domain.Entities;

public sealed record TwitchReward(string Id, string Title, int Cost)
{
    public string Display => $"{Title} ({Cost})";
}
