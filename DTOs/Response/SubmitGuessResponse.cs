using GuessingGame.API.Models.Enums;

namespace GuessingGame.API.DTOs.Response
{
    public class SubmitGuessResponse
    {
        public int GuessId { get; set; }
        public int GameId { get; set; }
        public int PlayerId { get; set; }
        public int SubmittedRound { get; set; }
        public bool IsRollupGuess { get; set; }
        public bool RoundEvaluated { get; set; }
        public int CurrentRound { get; set; }
        public GameStatus GameStatus { get; set; }
        public int RequiredPlayers { get; set; }
        public int SubmittedPlayers { get; set; }
        public int? FinalWinnerPlayerId { get; set; }
        public List<int> RoundWinnerPlayerIds { get; set; } = new();
    }
}
