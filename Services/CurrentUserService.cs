using System.Security.Claims;

namespace GuessingGame.API.Services;

public static class CurrentUserService
{
    public static int GetUserId(ClaimsPrincipal principal)
    {
        string? value = principal.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!int.TryParse(value, out int userId))
        {
            throw new UnauthorizedAccessException("The access token has no valid user ID.");
        }

        return userId;
    }

    public static int GetPlayerId(ClaimsPrincipal principal)
    {
        string? value = principal.FindFirstValue("playerId");

        if (!int.TryParse(value, out int playerId))
        {
            throw new UnauthorizedAccessException("The access token has no valid player ID.");
        }

        return playerId;
    }
}