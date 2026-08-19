using GuessingGame.API.DTOs.Request;
using GuessingGame.API.DTOs.Response;
using GuessingGame.API.Models;
using GuessingGame.API.Models.Enums;
using GuessingGame.API.Repositories.Interfaces;
using GuessingGame.API.Services.Interfaces;

namespace GuessingGame.API.Services
{
    public class GameService : IGameService
    {
        private readonly IGameRepository _games;
        private readonly IPlayerRepository _players;

        public GameService(IGameRepository games, IPlayerRepository players)
        {
            _games = games;
            _players = players;
        }

        public async Task<ApiResponse<CreateGameResponse>> CreateGameAsync(CreateGameRequest request)
        {
            if (!Enum.IsDefined(request.GameType))
                return Fail<CreateGameResponse>("Use 1 for Easy, 2 for Medium, or 3 for Hard and 99 for Random.");

            if (string.IsNullOrWhiteSpace(request.PlayerName))
                return Fail<CreateGameResponse>("Player name is required");

            if (request.stake <= 0)
                return Fail<CreateGameResponse>("Stake must be greater than zero");

            GameType selectedGame = request.GameType == GameType.Random ? (GameType)Random.Shared.Next(1, 4) : request.GameType;
            GameConfig config = GameSettings.GetConfig(selectedGame);

            string name = request.PlayerName.Trim();

            Player? player = await _players.GetByNameAsync(name);

            if (player is null)
            {
                player = new Player { Name = char.ToUpper(name[0]) + name[1..].ToLower() };
                await _players.AddAsync(player);
                await _players.SaveChangesAsync();
            }

            if (player.Balance < request.stake)
                return Fail<CreateGameResponse>($"{player.Name} has insufficient balance.");


            GameSession created = await _games.SaveGame(request, config, player, selectedGame);
            return Ok("Game created. Waiting for other players to join.", MapCreatedGame(created));
        }

        public async Task<ApiResponse<GameStateResponse>> GetGameAsync(int gameId)
        {
            GameSession game = await _games.GetByIdAsync(gameId);

            if (game is null)
                return Fail<GameStateResponse>("Game not found.");

            return Ok("Game retrieved successfully.", MapState(game));
        }

        public async Task<ApiResponse<GameStateResponse>> JoinGameAsync(int gameId, JoinGameRequest request)
        {
            GameSession? game =  await _games.GetByIdAsync(gameId);

            if (game is null)
            {
                return Fail<GameStateResponse>("Game not found.");
            }

            if (game.Status != GameStatus.WaitingForPlayers)
            {
                return Fail<GameStateResponse>("Players cannot join after the game has started.");
            }

            if (string.IsNullOrWhiteSpace(request.PlayerName))
            {
                return Fail<GameStateResponse>("Player name is required.");
            }

            if (request.Stake <= 0)
            {
                return Fail<GameStateResponse>("Stake must be greater than zero.");
            }

            GameConfig config = GameSettings.GetConfig(game.GameType);

            if (game.Players.Count >= config.MaxPlayers)
            {
                return Fail<GameStateResponse>($"The game already has the maximum of " + $"{config.MaxPlayers} players.");
            }

            string name = request.PlayerName.Trim();

            Player? player = await _players.GetByNameAsync(name);

            if (player is null)
            {
                player = new Player
                {
                    Name = char.ToUpper(name[0]) + name[1..].ToLower()
                };

                await _players.AddAsync(player);
                await _players.SaveChangesAsync();
            }

            bool alreadyJoined = game.Players.Any(gamePlayer => gamePlayer.PlayerId == player.Id);

            if (alreadyJoined)
            {
                return Fail<GameStateResponse>($"{player.Name} has already joined this game.");
            }

            if (player.Balance < request.Stake)
            {
                return Fail<GameStateResponse>($"{player.Name} has insufficient balance.");
            }

            var gamePlayer = new GamePlayer
            {
                GameSessionId = game.Id,
                PlayerId = player.Id,
                Stake = request.Stake,
                Status = PlayerStatus.Active
            };

            _games.AddGamePlayer(gamePlayer);
            await _games.SaveChangesAsync();

            GameSession updatedGame = (await _games.GetByIdAsync(gameId))!;

            return Ok($"{player.Name} joined the game successfully.", MapState(updatedGame));
        }

        public async Task<ApiResponse<GameStateResponse>> StartGameAsync(int gameId)
        {
            GameSession game = await _games.GetByIdAsync(gameId);

            if (game is null)
                return Fail<GameStateResponse>("Game not found.");

            if (game.Status != GameStatus.WaitingForPlayers)
                return Fail<GameStateResponse>($"Game cannot be started with status: {game.Status}");

            GameConfig config = GameSettings.GetConfig(game.GameType);

            if (game.Players.Count < config.MinPlayers)
            {
                return Fail<GameStateResponse>($"Game cannot be started with less than {config.MinPlayers} players.");
            }

            if (game.Players.Count > config.MaxPlayers)
            {
                return Fail<GameStateResponse>($"Game cannot be started with more than {config.MaxPlayers} players.");
            }

            if (game.Players.Any(x => x.Player.Balance < x.Stake))
                return Fail<GameStateResponse>("One or more players have insufficient balance to start the game.");

            foreach (GamePlayer entry in game.Players)
            {
                entry.Player.Balance -= entry.Stake;
                entry.Player.LastSeen = DateTime.UtcNow;
            }

            game.CurrentRound = 1;
            game.Status = GameStatus.WaitingForGuesses;
            game.StartedAt = DateTime.UtcNow;

            await _games.SaveChangesAsync();

            return Ok("Game started successfully. Round 1 is active.", MapState(game));
        }

        public async Task<ApiResponse> CancelGameAsync(int gameId)
        {
            GameSession? game = await _games.GetByIdAsync(gameId);

            if (game is null)
                return new ApiResponse { Success = false, Message = "Game not found." };

            if (game.Status == GameStatus.Completed)
                return new ApiResponse { Success = false, Message = "Game has already been completed and cannot be canceled." };

            if (game.Status == GameStatus.Cancelled)
                return new ApiResponse { Success = false, Message = "Game has already been canceled." };

            if (game.StartedAt.HasValue)
            {
                foreach (GamePlayer entry in game.Players)
                {
                    entry.Player.Balance += entry.Stake;
                }
            }

            game.Status = GameStatus.Cancelled;
            game.CompletedAt = DateTime.UtcNow;
            await _games.SaveChangesAsync();

            return new ApiResponse { Success = true, Message = "Game canceled successfully." };
        }
        private static CreateGameResponse MapCreatedGame(GameSession game) => new()
        {
            GameId = game.Id,
            GameType = game.GameType,
            Status = game.Status,
            CurrentRound = game.CurrentRound,
            Attempts = game.Attempts,
            GuessLength = game.GuessLength,
            Players = game.Players.Select(MapPlayer).ToList()
        };

        private static GamePlayerResponse MapPlayer(GamePlayer entry) => new()
        {
            GamePlayerId = entry.Id,
            PlayerId = entry.PlayerId,
            PlayerName = entry.Player.Name,
            Stake = entry.Stake,
            Status = entry.Status,
            Score = entry.Score,
            Winnings = entry.Winnings,
            WinningRound = entry.WinningRound
        };

        internal static GameStateResponse MapState(GameSession game) => new()
        {
            GameId = game.Id,
            GameType = game.GameType,
            Status = game.Status,
            CurrentRound = game.CurrentRound,
            Attempts = game.Attempts,
            GuessLength = game.GuessLength,
            AllowRollup = game.AllowRollup,
            RollupRound = game.RollupRound,
            WinningNumbers = game.Status == GameStatus.Completed ? game.WinningNumbers : null,
            CreatedAt = game.CreatedAt,
            StartedAt = game.StartedAt,
            CompletedAt = game.CompletedAt,
            Players = game.Players.Select(MapPlayer).ToList()
        };

        private static ApiResponse<T> Ok<T>(string message, T data) =>
            new() { Success = true, Message = message, Data = data };

        private static ApiResponse<T> Fail<T>(string message) =>
            new() { Success = false, Message = message };
    }
}