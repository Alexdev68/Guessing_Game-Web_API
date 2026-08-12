using GuessingGame.API.DTOs.Request;
using GuessingGame.API.DTOs.Response;
using GuessingGame.API.Models;
using GuessingGame.API.Models.Enums;
using GuessingGame.API.Repositories.Interfaces;
using GuessingGame.API.Services.Interfaces;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;

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

            GameType selectedGame = request.GameType == GameType.Random ? (GameType)Random.Shared.Next(1, 4) : request.GameType;
            GameConfig config = GameSettings.GetConfig(selectedGame);

            if (request.Players.Count < config.MinPlayers || request.Players.Count > config.MaxPlayers)
                return Fail<CreateGameResponse>($"Players must be between {config.MinPlayers} and {config.MaxPlayers} for {request.GameType} game mode");

            if (request.Players.Any(x => string.IsNullOrWhiteSpace(x.PlayerName) || x.stake <= 0))
                return Fail<CreateGameResponse>("Players must have a name and a stake greater than zero");

            bool duplicateNames = request.Players.GroupBy(x => x.PlayerName.Trim(), StringComparer.OrdinalIgnoreCase).Any(group => group.Count() > 1);

            if (duplicateNames)
                return Fail<CreateGameResponse>("A player can not be entered twice");

            var confirmedPlayers = new List<(Player player, decimal stake)>();

            foreach (var input in request.Players)
            {
                string name = input.PlayerName.Trim();
                Player player = await _players.GetByNameAsync(name);

                if (player is null)
                {
                    player = new Player { Name = char.ToUpper(name[0]) + name[1..].ToLower() };
                    await _players.AddAsync(player);
                    await _players.SaveChangesAsync();
                }

                if (player.Balance < input.stake)
                    return Fail<CreateGameResponse>($"{player.Name} has insufficient balance");

                confirmedPlayers.Add((player, input.stake));
            }

            var game = new GameSession
            {
                GameType = selectedGame,
                CurrentRound = 0,
                Attempts = config.Attempts,
                GuessLength = config.GuessLength,
                MinValue = config.MinValue,
                MaxValue = config.MaxValue,
                Multiplier = config.Multiplier,
                AllowRollup = config.AllowRollup,
                AllowAlphanumeric = config.AllowAlphanumeric,
                AllowDuplicates = config.AllowDuplicates,
                WinningNumbers = string.Join(", ", RandomGenerator.Generate(config))
            };

            await _games.AddAsync(game);
            await _games.SaveChangesAsync();

            foreach ((Player player, decimal stake) in confirmedPlayers)
            {
                _games.AddGamePlayer(new GamePlayer
                {
                    GameSessionId = game.Id,
                    PlayerId = player.Id,
                    Stake = stake,
                    Status = PlayerStatus.Active
                });
            }


            await _games.SaveChangesAsync();

            GameSession created = (await _games.GetByIdAsync(game.Id));
            return Ok("Game created Successfully.", MapCreatedGame(created));
        }

        public async Task<ApiResponse<GameStateResponse>> GetGameAsync(int gameId)
        {
            GameSession game = await _games.GetByIdAsync(gameId);

            if (game is null)
                return Fail<GameStateResponse>("Game not found.");

            return Ok("Game retrieved successfully.", MapState(game));
        }

        public async Task<ApiResponse<GameStateResponse>> StartGameAsync(int gameId)
        {
            GameSession game = await _games.GetByIdAsync(gameId);

            if (game is null)
                return Fail<GameStateResponse>("Game not found.");

            if (game.Status != GameStatus.WaitingForGuesses)
                return Fail<GameStateResponse>($"Game cannot be started with status: {game.Status}");

            if (game.Players.Any(x => x.Player.Balance < x.Stake))
                return Fail<GameStateResponse>("One or more players have insufficient balance to start the game.");

            foreach (GamePlayer entry in game.Players)
            {
                entry.Player.Balance -= entry.Stake;
                entry.Player.LastSeen = DateTime.UtcNow;
            }

            game.CurrentRound = 1;
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