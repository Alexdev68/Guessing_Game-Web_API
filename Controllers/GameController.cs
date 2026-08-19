using GuessingGame.API.DTOs.Request;
using GuessingGame.API.DTOs.Response;
using GuessingGame.API.Models.Enums;
using GuessingGame.API.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace GuessingGame.API.Controllers
{
    [Route("api/games")]
    [ApiController]
    public class GameController : ControllerBase
    {
        private readonly IGameService _games;
        public GameController(IGameService games) => _games = games;

        [HttpPost("create")]
        public async Task<IActionResult> CreateGame(CreateGameRequest request)
        {
            ApiResponse<CreateGameResponse> result = await _games.CreateGameAsync(request);

            return result.Success
                ? CreatedAtAction(nameof(GetGame), new { gameId = result.Data!.GameId }, result)
                : BadRequest(result);
        }

        [HttpPost("{gameId:int}/players")]
        public async Task<IActionResult> JoinGame([FromRoute] int gameId, [FromBody] JoinGameRequest request)
        {
            ApiResponse<GameStateResponse> result = await _games.JoinGameAsync(gameId, request);

            return result.Success ? Ok(result) : BadRequest(result);
        }

        [HttpPost("{gameId:int}/start")]
        public async Task<IActionResult> StartGame([FromRoute] int gameId)
        {
            ApiResponse<GameStateResponse> result = await _games.StartGameAsync(gameId);

            return result.Success ? Ok(result) : BadRequest(result);
        }

        [HttpGet("{gameId:int}")]
        public async Task<IActionResult> GetGame([FromRoute] int gameId)
        {
            ApiResponse<GameStateResponse> result = await _games.GetGameAsync(gameId);

            return result.Success ? Ok(result) : NotFound(result);
        }

        [HttpGet("{gameId:int}/results")]
        public async Task<IActionResult> GetResult([FromRoute] int gameId)
        {
            ApiResponse<GameStateResponse> result = await _games.GetGameAsync(gameId);

            if (!result.Success)
                return NotFound(result);

            if (result.Data!.Status != GameStatus.Completed)
                return BadRequest(new ApiResponse { Success = false, Message = "Game is not completed yet" });

            return Ok(result);
        }

        [HttpPost("{gameId:int}/cancel")]
        public async Task<IActionResult> CancelGame([FromRoute] int gameId)
        {
            ApiResponse result = await _games.CancelGameAsync(gameId);

            return result.Success ? Ok(result) : BadRequest(result);
        }
    }
}