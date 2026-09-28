using GuessingGame.API.Models.Enums;

namespace GuessingGame.API.Caching;

public static class CacheKeys
{
    public const string AvailableGames = "games:available";

    public static string Game(int gameId) => $"game:{gameId}";

    public static string PlayerGames(int playerId, PlayerGamesFilter filter) => $"player:{playerId}:games:{filter}";
}