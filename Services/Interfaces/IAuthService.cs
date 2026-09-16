using GuessingGame.API.DTOs.Request;
using GuessingGame.API.DTOs.Response;

namespace GuessingGame.API.Services.Interfaces;

public interface IAuthService
{
    Task<ApiResponse<TokenResponse>> RegisterAsync(RegisterRequest request);

    Task<ApiResponse<TokenResponse>> LoginAsync(LoginRequest request);

    Task<ApiResponse<TokenResponse>> RefreshTokenAsync(RefreshTokenRequest request);

    Task<ApiResponse> LogoutAsync(int userId);
}