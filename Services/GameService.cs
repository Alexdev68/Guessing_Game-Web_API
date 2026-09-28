using GuessingGame.API.DTOs.Request;
using GuessingGame.API.DTOs.Response;
using GuessingGame.API.Models;
using GuessingGame.API.Models.Enums;
using GuessingGame.API.Repositories.Interfaces;
using GuessingGame.API.Services.Interfaces;
using GuessingGame.API.Caching;
using Microsoft.Extensions.Caching.Memory;

namespace GuessingGame.API.Services
{
    public class GameService : IGameService
    {
        private readonly IGameRepository _games;
        private readonly IPlayerRepository _players;
        private readonly ILogger<GameService> _logger;
        private readonly IMemoryCache _cache;

        public GameService(IGameRepository games, IPlayerRepository players, ILogger<GameService> logger, IMemoryCache cache)
        {
            _games = games;
            _players = players;
            _logger = logger;
            _cache = cache;
        }

        public async Task<ApiResponse<CreateGameResponse>> CreateGameAsync(int authenticatedPlayerId, CreateGameRequest request)
        {
            if (!Enum.IsDefined(request.GameType))
            {
                _logger.LogWarning("Player {PlayerId} attempted to create a game with an invalid game type: {GameType}", authenticatedPlayerId, request.GameType);
                return Fail<CreateGameResponse>("Use 1 for Easy, 2 for Medium, or 3 for Hard and 99 for Random.");
            }

            if (request.Stake <= 0)
            {
                _logger.LogWarning("Player {PlayerId} attempted to create a game with an invalid stake: {Stake}", authenticatedPlayerId, request.Stake);
                return Fail<CreateGameResponse>("Stake must be greater than zero");
            }

            GameType selectedGame = request.GameType == GameType.Random ? (GameType)Random.Shared.Next(1, 4) : request.GameType;
            GameConfig config = GameSettings.GetConfig(selectedGame);

            Player? player = await _players.GetByIdAsync(authenticatedPlayerId);

            if (player is null)
            {
                _logger.LogWarning("Player {PlayerId} not found when attempting to create a game.", authenticatedPlayerId);
                return Fail<CreateGameResponse>("Player not found");
            }

            if (player.Balance < request.Stake)
            {
                _logger.LogWarning("Player {PlayerId} has insufficient balance.", authenticatedPlayerId);
                return Fail<CreateGameResponse>($"{player.Name} has insufficient balance.");
            }


            GameSession created = await _games.SaveGame(request, config, player, selectedGame);

            RemoveGameRelatedCaches(created.Id, new[] { authenticatedPlayerId });

            _logger.LogInformation("Player {PlayerId} created a new game with ID {GameId} with GameType {GameType} and stake {Stake}.", authenticatedPlayerId, created.Id, selectedGame, request.Stake);
            return Ok("Game created. Waiting for other players to join.", MapCreatedGame(created));
        }

        public async Task<ApiResponse<GameStateResponse>> GetGameAsync(int gameId)
        {
            string cacheKey = CacheKeys.Game(gameId);

            if (_cache.TryGetValue(cacheKey, out GameStateResponse? cachedGame))
            {
                _logger.LogDebug("Cache hit for game {GameId}.", gameId);
                return Ok("Game retrieved successfully from cache.", cachedGame!);
            }

            _logger.LogDebug("Cache miss for game {GameId}. Retrieving from database.", gameId);

            GameSession? game = await _games.GetByIdAsync(gameId);

            if (game is null)
            {
                _logger.LogInformation("Game with ID {GameId} not found.", gameId);
                return Fail<GameStateResponse>("Game not found.");
            }

            GameStateResponse response = MapState(game);

            _cache.Set(cacheKey, response, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = CacheDurations.Game,
                SlidingExpiration = TimeSpan.FromSeconds(10)
            });

            _logger.LogDebug("Retrieved game with ID {GameId}.", gameId);
            return Ok("Game retrieved successfully.", response);
        }

        public async Task<ApiResponse<GameStateResponse>> JoinGameAsync(int gameId, int authenticatedPlayerId, JoinGameRequest request)
        {
            GameSession? game =  await _games.GetByIdAsync(gameId);

            if (game is null)
            {
                _logger.LogWarning("Player {PlayerId} attempted to join a non-existent game with ID {GameId}.", authenticatedPlayerId, gameId);
                return Fail<GameStateResponse>("Game not found.");
            }

            if (game.Status != GameStatus.WaitingForPlayers)
            {
                _logger.LogWarning("Player {PlayerId} attempted to join a game with ID {GameId} with status {GameStatus}.", authenticatedPlayerId, gameId, game.Status);
                return Fail<GameStateResponse>("Players cannot join after the game has started.");
            }

            if (request.Stake <= 0)
            {
                _logger.LogWarning("Player {PlayerId} attempted to join a game with ID {GameId} with an invalid stake: {Stake}", authenticatedPlayerId, gameId, request.Stake);
                return Fail<GameStateResponse>("Stake must be greater than zero.");
            }

            GameConfig config = GameSettings.GetConfig(game.GameType);

            if (game.Players.Count >= config.MaxPlayers)
            {
                _logger.LogWarning("Player {PlayerId} attempted to join a full game with ID {GameId}.", authenticatedPlayerId, gameId);
                return Fail<GameStateResponse>($"The game is full, maximum of " + $"{config.MaxPlayers} players.");
            }

            Player? player = await _players.GetByIdAsync(authenticatedPlayerId);

            if (player is null)
            {
                _logger.LogWarning("Player {PlayerId} not found when attempting to join a game with ID {GameId}.", authenticatedPlayerId, gameId);
                return Fail<GameStateResponse>("Player not found.");
            }

            bool alreadyJoined = game.Players.Any(gamePlayer => gamePlayer.PlayerId == authenticatedPlayerId);

            if (alreadyJoined)
            {
                _logger.LogWarning("Player {PlayerId} attempted to join a game with ID {GameId} they have already joined.", authenticatedPlayerId, gameId);
                return Fail<GameStateResponse>($"{player.Name} has already joined this game.");
            }

            if (player.Balance < request.Stake)
            {
                _logger.LogWarning("Player {PlayerId} has insufficient balance when attempting to join a game with ID {GameId}.", authenticatedPlayerId, gameId);
                return Fail<GameStateResponse>($"{player.Name} has insufficient balance.");
            }

            var gamePlayer = new GamePlayer
            {
                GameSessionId = game.Id,
                PlayerId = authenticatedPlayerId,
                Stake = request.Stake,
                Status = PlayerStatus.Active
            };

            _games.AddGamePlayer(gamePlayer);
            await _games.SaveChangesAsync();

            RemoveGameRelatedCaches(gameId, game.Players
                .Select(entry => entry.PlayerId)
                .Append(authenticatedPlayerId));

            _logger.LogInformation("Player {PlayerId} joined game with ID {GameId} with stake {Stake}.", authenticatedPlayerId, gameId, request.Stake);
            GameSession? updatedGame = (await _games.GetByIdAsync(gameId));

            if (updatedGame is null)
            {
                return Fail<GameStateResponse>("Player joined, but the updated game could not be retrieved.");
            }

            return Ok($"{player.Name} joined the game successfully.", MapState(updatedGame));
        }

        public async Task<ApiResponse<GameStateResponse>> StartGameAsync(int gameId)
        {
            GameSession? game = await _games.GetByIdAsync(gameId);

            if (game is null)
            {
                _logger.LogWarning("Start game rejected because the game was not found.");
                return Fail<GameStateResponse>("Game not found.");
            }

            if (game.Status != GameStatus.WaitingForPlayers)
            {
                _logger.LogWarning("Start game rejected because game status is {GameStatus}.", game.Status);
                return Fail<GameStateResponse>($"Game cannot be started with status: {game.Status}");
            }

            GameConfig config = GameSettings.GetConfig(game.GameType);

            if (game.Players.Count < config.MinPlayers)
            {
                _logger.LogWarning("Start game rejected because there are not enough players. Current count: {PlayerCount}, Minimum required: {MinPlayers}.", game.Players.Count, config.MinPlayers);
                return Fail<GameStateResponse>($"Game cannot be started with less than {config.MinPlayers} players.");
            }

            if (game.Players.Count > config.MaxPlayers)
            {
                _logger.LogWarning("Start game rejected because there are too many players. Current count: {PlayerCount}, Maximum allowed: {MaxPlayers}.", game.Players.Count, config.MaxPlayers);
                return Fail<GameStateResponse>($"Game cannot be started with more than {config.MaxPlayers} players.");
            }

            if (game.Players.Any(x => x.Player.Balance < x.Stake))
            {
                _logger.LogWarning("Start game rejected because one or more players have insufficient balance. Player count: {PlayerCount}.", game.Players.Count);
                return Fail<GameStateResponse>("One or more players have insufficient balance to start the game.");
            }

            foreach (GamePlayer entry in game.Players)
            {
                entry.Player.Balance -= entry.Stake;
                entry.Player.LastSeen = DateTime.UtcNow;
            }

            game.CurrentRound = 1;
            game.Status = GameStatus.WaitingForGuesses;
            game.StartedAt = DateTime.UtcNow;

            await _games.SaveChangesAsync();

            RemoveGameRelatedCaches(gameId, game.Players.Select(entry => entry.PlayerId));

            _logger.LogInformation("Game with ID {GameId} started with {PlayerCount} players.", gameId, game.Players.Count);

            return Ok("Game started successfully. Round 1 is active.", MapState(game));
        }

        public async Task<ApiResponse<List<GameStateResponse>>> GetAvailableGamesAsync()
        {
            if (_cache.TryGetValue(CacheKeys.AvailableGames, out List<GameStateResponse>? cachedGames))
            {
                _logger.LogDebug("Cache hit for available games.");
                return Ok($"{cachedGames!.Count} available games retrieved successfully.", cachedGames);
            }

            _logger.LogDebug("Cache miss for available games. Fetching from database.");

            List<GameSession> waitingGames = await _games.GetWaitingGamesAsync();

            List<GameStateResponse> availableGames = waitingGames
                .Where(game =>
                {
                    GameConfig config = GameSettings.GetConfig(game.GameType);
                    return game.Players.Count < config.MaxPlayers;
                })
                .Select(game => MapState(game))
                .ToList();

            _cache.Set(CacheKeys.AvailableGames, availableGames, CacheDurations.AvailableGames);

            _logger.LogDebug("Retrieved {GameCount} available games for players to join.", availableGames.Count);
            return Ok($"{availableGames.Count} available games retrieved successfully.", availableGames);   
        }

        public async Task<ApiResponse<List<GameStateResponse>>> GetPlayerGamesAsync(int playerId, PlayerGamesFilter filter)
        {
            string cacheKeys = CacheKeys.PlayerGames(playerId, filter);
            
            if (_cache.TryGetValue(cacheKeys, out List<GameStateResponse>? cachedGames))
            {
                _logger.LogDebug("Cache hit for player {PlayerId} games with filter {Filter}.", playerId, filter);
                return Ok($"{cachedGames!.Count} {filter} game(s) retrieved successfully for player {playerId}.", cachedGames);
            }

            _logger.LogDebug("Cache miss for player {PlayerId} games with filter {Filter}. Fetching from database.", playerId, filter);

            Player? player = await _players.GetByIdAsync(playerId);

            if (player is null)
            {
                _logger.LogInformation("Player {PlayerId} not found when attempting to retrieve their games.", playerId);
                return Fail<List<GameStateResponse>>("Player not found.");
            }

            List<GameSession> games = await _games.GetGamesByPlayerIdAsync(playerId, filter);

            List<GameStateResponse> response = games .Select(game => MapState(game)).ToList();

            _cache.Set(cacheKeys, response, CacheDurations.PlayerGames);

            _logger.LogDebug("Retrieved Player {PlayerId} games.", playerId);

            return Ok( $"{response.Count} {filter} game(s) found for {player.Name}.", response);
        }

        public async Task<ApiResponse> CancelGameAsync(int gameId)
        {
            GameSession? game = await _games.GetByIdAsync(gameId);

            if (game is null)
            {
                _logger.LogWarning("Player attempted to cancel a game with ID {GameId} that was not found.", gameId);
                return new ApiResponse { Success = false, Message = "Game not found." };
            }

            if (game.Status == GameStatus.Completed)
            {
                _logger.LogWarning("Player attempted to cancel a game with ID {GameId} that has already been completed.", gameId);
                return new ApiResponse { Success = false, Message = "Game has already been completed and cannot be canceled." };
            }

            if (game.Status == GameStatus.Cancelled)
            {
                _logger.LogWarning("Player attempted to cancel a game with ID {GameId} that has already been canceled.", gameId);
                return new ApiResponse { Success = false, Message = "Game has already been canceled." };
            }

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

            RemoveGameRelatedCaches(gameId, game.Players.Select(entry => entry.PlayerId));

            _logger.LogInformation("Game with ID {GameId} has been canceled successfully.", gameId);

            return new ApiResponse { Success = true, Message = "Game canceled successfully." };
        }

        private void RemoveGameCache(int gameId)
        {
            _cache.Remove(
                CacheKeys.Game(gameId));
        }

        private void RemoveAvailableGamesCache()
        {
            _cache.Remove(CacheKeys.AvailableGames);
        }

        private void RemovePlayerGamesCache(int playerId)
        {
            foreach (PlayerGamesFilter filter in Enum.GetValues<PlayerGamesFilter>())
            {
                _cache.Remove(CacheKeys.PlayerGames(playerId, filter));
            }
        }

        private void RemoveGameRelatedCaches(int gameId, IEnumerable<int> playerIds)
        {
            RemoveGameCache(gameId);
            RemoveAvailableGamesCache();

            foreach (int playerId in playerIds.Distinct())
            {
                RemovePlayerGamesCache(playerId);
            }
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