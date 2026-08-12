using System.ComponentModel.DataAnnotations;

namespace GuessingGame.API.DTOs.Request
{
    public class SubmitGuessRequest
    {
        public int PlayerId { get; set; }
        public string Guesses { get; set; }
    }
}