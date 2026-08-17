using GuessingGame.API.DTOs.Request;
using GuessingGame.API.DTOs.Response;

namespace GuessingGame.API.Services.Interfaces
{
    public interface IGameService
    {
        public Task<ApiResponse<CreateGameResponse>> CreateGameAsync(CreateGameRequest request);
        Task<ApiResponse<GameStateResponse>> JoinGameAsync(int gameId, JoinGameRequest request);
        public Task<ApiResponse<GameStateResponse>> StartGameAsync(int gameId);
        public Task<ApiResponse<GameStateResponse>> GetGameAsync(int gameId);
        public Task<ApiResponse> CancelGameAsync(int gameId);
    }
}