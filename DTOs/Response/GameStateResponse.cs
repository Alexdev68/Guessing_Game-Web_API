using GuessingGame.API.Models.Enums;

namespace GuessingGame.API.DTOs.Response
{
    public class GameStateResponse
    {
        public int GameId { get; set; }
        public GameType GameType { get; set; }
        public GameStatus Status { get; set; }
        public int CurrentRound { get; set; }
        public int Attempts { get; set; }
        public int GuessLength { get; set; }
        public bool AllowRollup { get; set; }
        public int RollupRound { get; set; }
        public string WinningNumbers { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public List<GamePlayerResponse> Players { get; set; } = new();
    }
}
