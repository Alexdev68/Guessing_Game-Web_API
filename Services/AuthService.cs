using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using GuessingGame.API.Data;
using GuessingGame.API.DTOs.Request;
using GuessingGame.API.DTOs.Response;
using GuessingGame.API.Models;
using GuessingGame.API.Services.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace GuessingGame.API.Services;

public sealed class AuthService : IAuthService
{
    private readonly AppDbContext _context;
    private readonly IPasswordHasher<User> _passwordHasher;
    private readonly IConfiguration _configuration;

    public AuthService(AppDbContext context, IPasswordHasher<User> passwordHasher, IConfiguration configuration)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _configuration = configuration;
    }

    public async Task<ApiResponse<TokenResponse>> RegisterAsync(RegisterRequest request)
    {
        string email = request.Email.Trim().ToLowerInvariant();

        string playerName = request.PlayerName.Trim();

        bool emailExists = await _context.Users.AnyAsync(user => user.Email == email);

        if (emailExists)
        {
            return Fail<TokenResponse>("An account with this email already exists.");
        }

        bool playerNameExists = await _context.Players.AnyAsync(player => player.Name.ToLower() == playerName.ToLower());

        if (playerNameExists)
        {
            return Fail<TokenResponse>("That player name is already in use.");
        }

        await using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            var player = new Player
            {
                Name = FormatName(playerName),
                Balance = 5000,
                FirstSeen = DateTime.UtcNow,
                LastSeen = DateTime.UtcNow
            };

            _context.Players.Add(player);
            await _context.SaveChangesAsync();

            var user = new User
            {
                Email = email,
                Role = "Player",
                PlayerId = player.Id,
                Player = player
            };

            user.PasswordHash = _passwordHasher.HashPassword(user, request.Password);

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            TokenResponse tokenResponse = await CreateTokenResponseAsync(user);

            await transaction.CommitAsync();

            return Ok("Account registered successfully.", tokenResponse);
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<ApiResponse<TokenResponse>> LoginAsync(LoginRequest request)
    {
        string email = request.Email.Trim().ToLowerInvariant();

        User? user = await _context.Users
                .Include(user => user.Player)
                .FirstOrDefaultAsync(
                    user => user.Email == email);

        if (user is null)
        {
            return Fail<TokenResponse>("Invalid email or password.");
        }

        PasswordVerificationResult passwordResult = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);

        if (passwordResult == PasswordVerificationResult.Failed)
        {
            return Fail<TokenResponse>("Invalid email or password.");
        }

        if (passwordResult == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = _passwordHasher.HashPassword(user, request.Password);
        }

        TokenResponse tokenResponse = await CreateTokenResponseAsync(user);

        return Ok("Login successful.", tokenResponse);
    }

    public async Task<ApiResponse<TokenResponse>> RefreshTokenAsync(RefreshTokenRequest request)
    {
        User? user = await _context.Users
                .Include(user => user.Player)
                .FirstOrDefaultAsync(
                    user => user.RefreshToken == request.RefreshToken);

        if (user is null)
        {
            return Fail<TokenResponse>("User account not found.");
        }

        if (user.RefreshToken != request.RefreshToken)
        {
            return Fail<TokenResponse>("Invalid refresh token.");
        }

        if (user.RefreshTokenExpiryTime is null || user.RefreshTokenExpiryTime <= DateTime.UtcNow)
        {
            return Fail<TokenResponse>("Refresh token has expired.");
        }

        TokenResponse tokenResponse = await CreateTokenResponseAsync(user);

        return Ok("Tokens refreshed successfully.", tokenResponse);
    }

    public async Task<ApiResponse> LogoutAsync(int userId)
    {
        User? user = await _context.Users.FindAsync(userId);

        if (user is null)
        {
            return new ApiResponse
            {
                Success = false,
                Message = "User account not found."
            };
        }

        user.RefreshToken = null;
        user.RefreshTokenExpiryTime = null;

        await _context.SaveChangesAsync();

        return new ApiResponse
        {
            Success = true,
            Message = "Logout successful."
        };
    }

    private async Task<TokenResponse> CreateTokenResponseAsync(User user)
    {
        DateTime accessTokenExpiresAt = DateTime.UtcNow.AddMinutes(_configuration.GetValue<int>("Jwt:AccessTokenMinutes"));

        DateTime refreshTokenExpiresAt = DateTime.UtcNow.AddDays(_configuration.GetValue<int>("Jwt:RefreshTokenDays"));

        string accessToken = CreateJwtToken(user, accessTokenExpiresAt);

        string refreshToken = GenerateRefreshToken();

        user.RefreshToken = refreshToken;

        user.RefreshTokenExpiryTime = refreshTokenExpiresAt;

        await _context.SaveChangesAsync();

        return new TokenResponse
        {
            UserId = user.Id,
            PlayerId = user.PlayerId,
            PlayerName = user.Player.Name,
            Role = user.Role,
            AccessToken = accessToken,
            AccessTokenExpiresAt = accessTokenExpiresAt,
            RefreshToken = refreshToken,
            RefreshTokenExpiresAt = refreshTokenExpiresAt
        };
    }

    private string CreateJwtToken(User user, DateTime expiryTime)
    {
        List<Claim> claims = new()
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),

            new Claim(ClaimTypes.Email, user.Email),

            new Claim(ClaimTypes.Name, user.Player.Name),

            new Claim(ClaimTypes.Role, user.Role),

            new Claim("playerId", user.PlayerId.ToString()),

            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        string tokenKey = _configuration["Jwt:Token"] ?? throw new InvalidOperationException("JWT token key is missing.");

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(tokenKey));

        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha512);

        var tokenDescriptor = new JwtSecurityToken(
                issuer: _configuration["Jwt:Issuer"],

                audience: _configuration["Jwt:Audience"],

                claims: claims,

                expires: expiryTime,

                signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(tokenDescriptor);
    }

    private static string GenerateRefreshToken()
    {
        byte[] randomBytes = RandomNumberGenerator.GetBytes(64);

        return Convert.ToBase64String(randomBytes);
    }

    private static string FormatName(string name)
    {
        return char.ToUpperInvariant(name[0]) + name[1..].ToLowerInvariant();
    }

    private static ApiResponse<T> Ok<T>(string message, T data)
    {
        return new ApiResponse<T>
        {
            Success = true,
            Message = message,
            Data = data
        };
    }

    private static ApiResponse<T> Fail<T>(string message)
    {
        return new ApiResponse<T>
        {
            Success = false,
            Message = message
        };
    }
}