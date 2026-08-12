using GuessingGame.API.Models.Enums;
using System.ComponentModel.DataAnnotations;

namespace GuessingGame.API.DTOs.Request
{
    public class CreateGameRequest
    {
        [Required]
        public GameType GameType { get; set; }
        [Required, MinLength(1)]
        public List<CreateGamePlayerRequest> Players { get; set; } = new();
    }
}