using System.ComponentModel.DataAnnotations;

namespace GuessingGame.API.DTOs.Request
{
    public class SubmitGuessRequest
    {
        public string Guesses { get; set; } = string.Empty;
    }
}