using GuessingGame.API.Models.Enums;
using System.ComponentModel.DataAnnotations;

namespace GuessingGame.API.DTOs.Request
{
    public class CreateGameRequest
    {
        [Required]
        public GameType GameType { get; set; }

        [Range(typeof(decimal), "0.01", "79228162514264337593543950335")]
        public decimal Stake { get; set; }
    }
}