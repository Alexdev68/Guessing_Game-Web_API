using GuessingGame.API.Models.Enums;
using GuessingGame.API.Services;

namespace GuessingGame.API.DTOs.Response
{
    public class GamePlayerResponse
    {
        public int GamePlayerId { get; set; }
        public string PlayerName { get; set; } = string.Empty;
        public int PlayerId { get; set; }
        public decimal Stake { get; set; }
        public PlayerStatus Status { get; set; }
        public int Score { get; set; }
        public decimal Winnings { get; set; }
        public int? WinningRound { get; set; }
    }
}