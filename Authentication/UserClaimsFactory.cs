using System.Security.Claims;
using GuessingGame.API.Models;

namespace GuessingGame.API.Authentication;

public static class UserClaimsFactory
{
    public static List<Claim> Create(User user, string authenticationMethod)
    {
        return new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),

            new Claim(ClaimTypes.Email, user.Email),

            new Claim(ClaimTypes.Name, user.Player.Name),

            new Claim(ClaimTypes.Role, user.Role),

            new Claim("playerId", user.PlayerId.ToString()),

            new Claim("authenticationMethod", authenticationMethod)
        };
    }
}