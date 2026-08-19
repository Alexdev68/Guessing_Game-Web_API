using GuessingGame.API.Models.Enums;

namespace GuessingGame.API.Models
{
    public class GameSession
    {
        public int Id { get; set; }
        public GameType GameType { get; set; }
        public GameStatus Status { get; set; } = GameStatus.WaitingForGuesses;
        public int CurrentRound { get; set; }
        public int Attempts { get; set; }
        public int GuessLength { get; set; }
        public int MinValue { get; set; }
        public int MaxValue { get; set; }
        public decimal Multiplier { get; set; }
        public bool AllowRollup { get; set; }
        public bool AllowDuplicates { get; set; }
        public bool AllowAlphanumeric { get; set; }
        public string WinningNumbers { get; set; }
        public int RollupRound { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public int? FinalWinnerPlayerId { get; set; }
        public virtual ICollection<GamePlayer> Players { get; set; } = new List<GamePlayer>();
    }
}