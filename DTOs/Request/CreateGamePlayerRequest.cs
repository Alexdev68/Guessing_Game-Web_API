using System.ComponentModel.DataAnnotations;

namespace GuessingGame.API.DTOs.Request
{
    public class CreateGamePlayerRequest
    {
        [Required, MinLength(2)]
        public string PlayerName { get; set; } = String.Empty;

        [Range(typeof(decimal), "0.01", "79228162514264337593543950335")]
        public decimal stake { get; set; }
    }
}