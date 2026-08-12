using GuessingGame.API.Models.Enums;

namespace GuessingGame.API.DTOs.Response
{
    public class CreateGameResponse
    {
        public int GameId { get; set; }
        public GameType GameType { get; set; }
        public GameStatus Status { get; set; }
        public int CurrentRound { get; set; }
        public int Attempts { get; set; }
        public int GuessLength { get; set; }
        public List<GamePlayerResponse> Players { get; set; } = new();
    }
}