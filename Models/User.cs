namespace GuessingGame.API.Models;

public sealed class User
{
    public int Id { get; set; }

    public string Email { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;

    public string Role { get; set; } = "Player";

    public string? RefreshToken { get; set; }

    public DateTime? RefreshTokenExpiryTime { get; set; }

    public int PlayerId { get; set; }

    public Player Player { get; set; } = null!;
}