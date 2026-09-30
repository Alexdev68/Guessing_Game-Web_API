using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using GuessingGame.API.Data;
using GuessingGame.API.DTOs.Request;
using GuessingGame.API.DTOs.Response;
using GuessingGame.API.Models;
using GuessingGame.API.Services.Interfaces;
using GuessingGame.API.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace GuessingGame.API.Services;

public sealed class AuthService : IAuthService
{
    private readonly AppDbContext _context;
    private readonly IPasswordHasher<User> _passwordHasher;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AuthService> _logger;

    public AuthService(AppDbContext context, IPasswordHasher<User> passwordHasher, IConfiguration configuration, ILogger<AuthService> logger)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<ApiResponse<TokenResponse>> RegisterAsync(RegisterRequest request)
    {
        string email = request.Email.Trim().ToLowerInvariant();

        string playerName = request.PlayerName.Trim();

        bool emailExists = await _context.Users.AnyAsync(user => user.Email == email);

        if (emailExists)
        {
            _logger.LogWarning("Registration rejected because email is already registered.");

            return Fail<TokenResponse>("An account with this email already exists.");
        }

        bool playerNameExists = await _context.Players.AnyAsync(player => player.Name.ToLower() == playerName.ToLower());

        if (playerNameExists)
        {
            _logger.LogWarning("Registration rejected because player name is already in use.");

            return Fail<TokenResponse>("That player name is already in use.");
        }

        var executionStrategy = _context.Database.CreateExecutionStrategy();

        return await executionStrategy.ExecuteAsync(
            async () =>
            {
                _context.ChangeTracker.Clear();

                await using var transaction = await _context.Database.BeginTransactionAsync();

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

                _logger.LogInformation("User {UserId} registered successfully with Player {PlayerId}", user.Id, player.Id);

                return Ok("Account registered successfully.", tokenResponse);
            });
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
            _logger.LogWarning("Login attempt failed because the credentials were invalid.");
            return Fail<TokenResponse>("Invalid email or password.");
        }

        PasswordVerificationResult passwordResult = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);

        if (passwordResult == PasswordVerificationResult.Failed)
        {
            _logger.LogWarning("Login attempt failed for user {UserId}", user.Id);
            return Fail<TokenResponse>("Invalid email or password.");
        }

        if (passwordResult == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = _passwordHasher.HashPassword(user, request.Password);
        }

        TokenResponse tokenResponse = await CreateTokenResponseAsync(user);

        _logger.LogInformation("User {UserId} logged in successfully.", user.Id);

        return Ok("Login successful.", tokenResponse);
    }

    public async Task<ApiResponse<TokenResponse>> RefreshTokenAsync(RefreshTokenRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            _logger.LogWarning("Token refresh rejected because a refresh token was not supplied.");
            return Fail<TokenResponse>("Refresh token is required.");
        }

        string submittedTokenHash = SecurityTokenHelper.HashToken(request.RefreshToken);

        User? user = await _context.Users
                .Include(user => user.Player)
                .FirstOrDefaultAsync(
                    user => user.RefreshTokenHash == submittedTokenHash);

        if (user is null)
        {
            _logger.LogWarning("Token refresh rejected because the refresh token was invalid");
            return Fail<TokenResponse>("Invalid refresh token");
        }

        if (user.RefreshTokenExpiryTime is null || user.RefreshTokenExpiryTime <= DateTime.UtcNow)
        {
            _logger.LogWarning("Expired refresh token used by user {UserId}", user.Id);
            return Fail<TokenResponse>("Refresh token has expired.");
        }

        TokenResponse tokenResponse = await CreateTokenResponseAsync(user);

        _logger.LogInformation("Tokens refreshed successfully for user {UserId}", user.Id);

        return Ok("Tokens refreshed successfully.", tokenResponse);
    }

    public async Task<ApiResponse> LogoutAsync(int userId)
    {
        User? user = await _context.Users.FindAsync(userId);

        if (user is null)
        {
            _logger.LogWarning("Logout attempt failed because the user was not found.");
            return new ApiResponse
            {
                Success = false,
                Message = "User account not found."
            };
        }

        user.RefreshTokenHash = null;
        user.RefreshTokenExpiryTime = null;

        await _context.SaveChangesAsync();

        _logger.LogInformation("User {UserId} logged out successfully.", user.Id);

        return new ApiResponse
        {
            Success = true,
            Message = "Logout successful."
        };
    }

    public async Task<ApiResponse<ApiKeyResponse>> GenerateApiKeyAsync(int authenticatedUserId)
    {
        User? user = await _context.Users.FindAsync(authenticatedUserId);

        if (user is null)
        {
            _logger.LogWarning("API key generation failed because the user was not found.");
            return Fail<ApiKeyResponse>("User account was not found.");
        }

        string rawApiKey = SecurityTokenHelper.GenerateSecureToken();

        user.ApiKeyHash = SecurityTokenHelper.HashToken(rawApiKey);

        user.ApiKeyCreatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        _logger.LogInformation("API key generated successfully for user {UserId}", user.Id);

        var response = new ApiKeyResponse
        {
            ApiKey = rawApiKey,
            CreatedAt = user.ApiKeyCreatedAt.Value
        };

        return Ok("API key generated successfully. Store it securely because it will not be shown again.", response);
    }

    public async Task<ApiResponse> RevokeApiKeyAsync(int authenticatedUserId)
    {
        User? user = await _context.Users.FindAsync(authenticatedUserId);

        if (user is null)
        {
            _logger.LogWarning("API key revocation failed because the user was not found.");

            return new ApiResponse
            {
                Success = false,
                Message = "User account was not found."
            };
        }

        user.ApiKeyHash = null;
        user.ApiKeyCreatedAt = null;

        await _context.SaveChangesAsync();

        _logger.LogInformation("User {UserId} revoked an API key", user.Id);

        return new ApiResponse
        {
            Success = true,
            Message = "API key revoked successfully."
        };
    }

    private async Task<TokenResponse> CreateTokenResponseAsync(User user)
    {
        DateTime accessTokenExpiresAt = DateTime.UtcNow.AddMinutes(_configuration.GetValue<int>("Jwt:AccessTokenMinutes"));

        DateTime refreshTokenExpiresAt = DateTime.UtcNow.AddDays(_configuration.GetValue<int>("Jwt:RefreshTokenDays"));

        string accessToken = CreateJwtToken(user, accessTokenExpiresAt);

        string refreshToken = SecurityTokenHelper.GenerateSecureToken();

        user.RefreshTokenHash = SecurityTokenHelper.HashToken(refreshToken);

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
        List<Claim> claims = UserClaimsFactory.Create(user, "Jwt");

        claims.Add(new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()));

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