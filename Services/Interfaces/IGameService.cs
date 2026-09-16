using GuessingGame.API.DTOs.Request;
using GuessingGame.API.DTOs.Response;
using GuessingGame.API.Models.Enums;

namespace GuessingGame.API.Services.Interfaces
{
    public interface IGameService
    {
        public Task<ApiResponse<CreateGameResponse>> CreateGameAsync(int authenticatedPlayerId, CreateGameRequest request);
        Task<ApiResponse<GameStateResponse>> JoinGameAsync(int gameId, int authenticatedPlayerId, JoinGameRequest request);
        public Task<ApiResponse<GameStateResponse>> StartGameAsync(int gameId);
        public Task<ApiResponse<GameStateResponse>> GetGameAsync(int gameId);
        public Task<ApiResponse<List<GameStateResponse>>> GetAvailableGamesAsync();
        Task<ApiResponse<List<GameStateResponse>>> GetPlayerGamesAsync(int playerId, PlayerGamesFilter filter);
        public Task<ApiResponse> CancelGameAsync(int gameId);
    }
}