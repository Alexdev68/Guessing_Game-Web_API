using GuessingGame.API.Models.Enums;

namespace GuessingGame.API.Models
{
    public class GameConfig
    {
        public GameType Type { get; set; }

        public string color { get; set; }

        public int Attempts { get; set; }

        public int GuessLength { get; set; }

        public int MinPlayers { get; set; }

        public int MaxPlayers { get; set; }

        public int MinValue { get; set; }

        public int MaxValue { get; set; }

        public int Multiplier { get; set; }

        public bool AllowDuplicates { get; set; }

        public bool AllowRollup { get; set; }

        public bool AllowAlphanumeric { get; set; }
    }
}