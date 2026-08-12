using GuessingGame.API.Models.Enums;

namespace GuessingGame.API.Models
{
    public class GamePlayer
    {
        public int Id { get; set; }
        public int GameSessionId { get; set; }
        public int PlayerId { get; set; }
        public decimal Stake { get; set; }
        public int Score { get; set; }
        public decimal Winnings { get; set; }
        public int? WinningRound { get; set; }
        public PlayerStatus Status { get; set; } = PlayerStatus.Active;
        public GameSession GameSession { get; set; } = null!;
        public Player Player { get; set; } = null!;
        public ICollection<PlayerGuess> Guesses { get; set; } = new List<PlayerGuess>();
    }
}
