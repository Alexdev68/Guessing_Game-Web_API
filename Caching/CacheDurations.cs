namespace GuessingGame.API.Caching;

public static class CacheDurations
{
    public static readonly TimeSpan Game = TimeSpan.FromSeconds(30);

    public static readonly TimeSpan AvailableGames = TimeSpan.FromSeconds(20);

    public static readonly TimeSpan PlayerGames = TimeSpan.FromMinutes(1);
}