using GuessingGame.API.Models.Enums;

namespace GuessingGame.API.Models
{
    internal class GameSettings
    {
        public static GameConfig GetConfig(GameType type)
        {
            return AllGames[type];
        }

        public static Dictionary<GameType, GameConfig> AllGames = new()
        {
            { GameType.Easy, new GameConfig
            {
                    Type = GameType.Easy,
                    color = "green",
                    Attempts = 6,
                    GuessLength = 3,
                    MinPlayers = 2,
                    MaxPlayers = 10,
                    MinValue = 0,
                    MaxValue = 20,
                    Multiplier = 340,
                    AllowDuplicates = true,
                    AllowRollup = false,
                    AllowAlphanumeric = false
                }
            },
            { GameType.Medium, new GameConfig
                {
                    Type = GameType.Medium,
                    color = "yellow",
                    Attempts = 3,
                    GuessLength = 4,
                    MinPlayers = 2,
                    MaxPlayers = 10,
                    MinValue = 0,
                    MaxValue = 50,
                    Multiplier = 2250,
                    AllowDuplicates = false,
                    AllowRollup = true,
                    AllowAlphanumeric = false
                }
            },
            { GameType.Hard, new GameConfig
                {
                    Type = GameType.Hard,
                    color = "red",
                    Attempts = 1,
                    GuessLength = 5,
                    MinPlayers = 2,
                    MaxPlayers = 10,
                    MinValue = 0,
                    MaxValue = 100,
                    Multiplier = 10000,
                    AllowDuplicates = true,
                    AllowRollup = true,
                    AllowAlphanumeric = true
                }
            },
            { GameType.Random, new GameConfig
                {
                    Type = GameType.Random,
                    color = "magenta",
                }
            }
        };
    }
}