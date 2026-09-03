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

        /// <summary>
        /// A single player can create a new game session and become the host of the game.
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        [HttpPost("create")]
        public async Task<IActionResult> CreateGame(CreateGameRequest request)
        {
            ApiResponse<CreateGameResponse> result = await _games.CreateGameAsync(request);

            return result.Success
                ? CreatedAtAction(nameof(GetGame), new { gameId = result.Data!.GameId }, result)
                : BadRequest(result);
        }

        /// <summary>
        /// A player can join an existing game session by providing the game ID and their player information.
        /// </summary>
        /// <param name="gameId"></param>
        /// <param name="request"></param>
        /// <returns></returns>
        [HttpPost("{gameId:int}/join-game")]
        public async Task<IActionResult> JoinGame([FromRoute] int gameId, [FromBody] JoinGameRequest request)
        {
            ApiResponse<GameStateResponse> result = await _games.JoinGameAsync(gameId, request);

            return result.Success ? Ok(result) : BadRequest(result);
        }

        /// <summary>
        /// A player of the game can start the game session once all players have joined.
        /// </summary>
        /// <param name="gameId"></param>
        /// <returns></returns>
        [HttpPost("{gameId:int}/start")]
        public async Task<IActionResult> StartGame([FromRoute] int gameId)
        {
            ApiResponse<GameStateResponse> result = await _games.StartGameAsync(gameId);

            return result.Success ? Ok(result) : BadRequest(result);
        }


        /// <summary>
        /// A player can retrieve all games that are available to join.
        /// </summary>
        /// <returns></returns>
        [HttpGet("available-games")]
        public async Task<IActionResult> GetAvailableGames()
        {
            ApiResponse<List<GameStateResponse>> result = await _games.GetAvailableGamesAsync();
            return Ok(result);
        }

        /// <summary>
        /// A player can retrieve all games they are a part of, including games that are not started yet or active in or completed.
        /// </summary>
        /// <param name="playerId"></param>
        /// <param name="filter"></param>
        /// <returns></returns>
        [HttpGet("{playerId:int}/games")]
        public async Task<IActionResult> GetPlayerGames(int playerId, [FromQuery] PlayerGamesFilter filter = PlayerGamesFilter.All)
        {
            ApiResponse<List<GameStateResponse>> result = await _games.GetPlayerGamesAsync(playerId, filter);

            return result.Success ? Ok(result) : NotFound(result);
        }

        /// <summary>
        /// A player can retrieve the current state of a specific game session by providing the game ID.
        /// </summary>
        /// <param name="gameId"></param>
        /// <returns></returns>
        [HttpGet("{gameId:int}")]
        public async Task<IActionResult> GetGame([FromRoute] int gameId)
        {
            ApiResponse<GameStateResponse> result = await _games.GetGameAsync(gameId);

            return result.Success ? Ok(result) : NotFound(result);
        }

        /// <summary>
        /// This endpoint retrieves a game that has been completed by providing the game ID.
        /// </summary>
        /// <param name="gameId"></param>
        /// <returns></returns>
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


        /// <summary>
        /// This endpoint cancels a game that has not been completed or cancelled.
        /// </summary>
        /// <param name="gameId"></param>
        /// <returns></returns>
        [HttpPost("{gameId:int}/cancel")]
        public async Task<IActionResult> CancelGame([FromRoute] int gameId)
        {
            ApiResponse result = await _games.CancelGameAsync(gameId);

            return result.Success ? Ok(result) : BadRequest(result);
        }
    }
}