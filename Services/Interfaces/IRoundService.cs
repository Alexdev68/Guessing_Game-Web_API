using GuessingGame.API.DTOs.Request;
using GuessingGame.API.DTOs.Response;

namespace GuessingGame.API.Services.Interfaces
{
    public interface IRoundService
    {
        Task<ApiResponse<SubmitGuessResponse>> SubmitGuessAsync(int gameId, SubmitGuessRequest request);
        Task<ApiResponse<SubmitGuessResponse>> SubmitRollupGuessAsync(int gameId, SubmitGuessRequest request);
    }
}