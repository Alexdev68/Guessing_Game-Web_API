using GuessingGame.API.DTOs.Request;
using GuessingGame.API.DTOs.Response;
using GuessingGame.API.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using GuessingGame.API.Services;

namespace GuessingGame.API.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/games/{gameId:int}")]
    public sealed class RoundsController : ControllerBase
    {
        private readonly IRoundService _rounds;
        public RoundsController(IRoundService rounds) => _rounds = rounds;

        /// <summary>
        /// This endpoint collects guesses for normal rounds.
        /// </summary>
        /// <param name="gameId"></param>
        /// <param name="request"></param>
        /// <returns></returns>
        [HttpPost("guesses")]
        public async Task<IActionResult> SubmitGuess(int gameId, [FromBody] SubmitGuessRequest request)
        {
            int playerId = CurrentUserService.GetPlayerId(User);
            ApiResponse<SubmitGuessResponse> result = await _rounds.SubmitGuessAsync(gameId, playerId, request);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        /// <summary>
        /// This endpoint collects guesses for rollup rounds.
        /// </summary>
        /// <param name="gameId"></param>
        /// <param name="request"></param>
        /// <returns></returns>
        [HttpPost("rollup/guesses")]
        public async Task<IActionResult> SubmitRollupGuess(int gameId, [FromBody] SubmitGuessRequest request)
        {
            int playerId = CurrentUserService.GetPlayerId(User);
            ApiResponse<SubmitGuessResponse> result = await _rounds.SubmitRollupGuessAsync(gameId, playerId, request);
            return result.Success ? Ok(result) : BadRequest(result);
        }
    }
}
