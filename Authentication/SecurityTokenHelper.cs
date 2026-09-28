using System.Security.Cryptography;
using System.Text;

namespace GuessingGame.API.Authentication;

public static class SecurityTokenHelper
{
    public static string GenerateSecureToken()
    {
        byte[] randomBytes = RandomNumberGenerator.GetBytes(64);

        return Convert.ToBase64String(randomBytes);
    }

    public static string HashToken(string token)
    {
        byte[] tokenBytes = Encoding.UTF8.GetBytes(token);

        byte[] hash = SHA256.HashData(tokenBytes);

        return Convert.ToHexString(hash);
    }
}