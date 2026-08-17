namespace GuessingGame.API.DTOs.Request;

public class JoinGameRequest
{
    public string PlayerName { get; set; } = string.Empty;
    public decimal Stake { get; set; }
}