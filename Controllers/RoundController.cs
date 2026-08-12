using GuessingGame.API.DTOs.Request;
using GuessingGame.API.DTOs.Response;
using GuessingGame.API.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace GuessingGame.API.Controllers
{
    [ApiController]
    [Route("api/games/{gameId:int}")]
    public sealed class RoundsController : ControllerBase
    {
        private readonly IRoundService _rounds;
        public RoundsController(IRoundService rounds) => _rounds = rounds;

        [HttpPost("guesses")]
        public async Task<IActionResult> SubmitGuess(
            int gameId, [FromBody] SubmitGuessRequest request)
        {
            ApiResponse<SubmitGuessResponse> result =
                await _rounds.SubmitGuessAsync(gameId, request);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        [HttpPost("rollup/guesses")]
        public async Task<IActionResult> SubmitRollupGuess(
            int gameId, [FromBody] SubmitGuessRequest request)
        {
            ApiResponse<SubmitGuessResponse> result =
                await _rounds.SubmitRollupGuessAsync(gameId, request);
            return result.Success ? Ok(result) : BadRequest(result);
        }
    }
}
