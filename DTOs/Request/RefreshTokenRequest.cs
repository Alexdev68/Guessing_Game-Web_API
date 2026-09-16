using System.ComponentModel.DataAnnotations;

namespace GuessingGame.API.DTOs.Request;

public sealed class RefreshTokenRequest
{
    [Required]
    public string RefreshToken { get; set; } = string.Empty;
}