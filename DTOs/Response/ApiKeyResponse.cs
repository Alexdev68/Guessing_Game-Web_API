namespace GuessingGame.API.DTOs.Response;

public sealed class ApiKeyResponse
{
    public string ApiKey { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
}