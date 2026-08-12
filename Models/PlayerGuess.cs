namespace GuessingGame.API.Models
{
    public class PlayerGuess
    {
        public int Id { get; set; }
        public int GamePlayerId { get; set; }
        public int RoundNumber { get; set; }
        public string GuessValues { get; set; } = string.Empty;
        public bool IsRollupGuess { get; set; }
        public bool IsCorrect { get; set; }
        public int MatchCount { get; set; }
        public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;
        public GamePlayer GamePlayer { get; set; } = null!;
    }
}
