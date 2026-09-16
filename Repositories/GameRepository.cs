using GuessingGame.API.Data;
using GuessingGame.API.DTOs.Request;
using GuessingGame.API.Models;
using GuessingGame.API.Models.Enums;
using GuessingGame.API.Repositories.Interfaces;
using GuessingGame.API.Services;
using Microsoft.EntityFrameworkCore;

namespace GuessingGame.API.Repositories
{
    public class GameRepository : IGameRepository
    {
        private readonly AppDbContext _context;
        public GameRepository(AppDbContext context) => _context = context;


        public async Task AddAsync(GameSession game) =>
            await _context.GameSessions.AddAsync(game);
        public void AddGamePlayer(GamePlayer gamePlayer) => _context.GamePlayers.Add(gamePlayer);

        public void AddGuess(PlayerGuess guess) => _context.PlayerGuesses.Add(guess);

        public Task<GameSession?> GetByIdAsync(int gameId) => _context.GameSessions
            .Include(game => game.Players)
                .ThenInclude(gamePlayer => gamePlayer.Player)
            .Include(game => game.Players)
                .ThenInclude(gamePlayer => gamePlayer.Guesses)
            .FirstOrDefaultAsync(game => game.Id == gameId);

        public Task<bool> GuessExistsAsync(int gamePlayerId, int roundNumber, bool isRollupGuess) =>
            _context.PlayerGuesses.AnyAsync(x =>
                x.GamePlayerId == gamePlayerId &&
                x.RoundNumber == roundNumber &&
                x.IsRollupGuess == isRollupGuess);

        public Task SaveChangesAsync() => _context.SaveChangesAsync();

        public async Task<GameSession> SaveGame(CreateGameRequest request, GameConfig config, Player player, GameType selectedGame)
        {
            var game = new GameSession
            {
                GameType = selectedGame,
                Status = GameStatus.WaitingForPlayers,
                CurrentRound = 0,
                Attempts = config.Attempts,
                GuessLength = config.GuessLength,
                MinValue = config.MinValue,
                MaxValue = config.MaxValue,
                Multiplier = config.Multiplier,
                AllowRollup = config.AllowRollup,
                AllowAlphanumeric = config.AllowAlphanumeric,
                AllowDuplicates = config.AllowDuplicates,
                WinningNumbers = string.Join(", ", RandomGenerator.Generate(config)),
                CreatedAt = DateTime.UtcNow
            };

            game.Players.Add(new GamePlayer
            {
                PlayerId = player.Id,
                Player = player,
                Stake = request.Stake,
                Status = PlayerStatus.Active
            });

            await _context.GameSessions.AddAsync(game);

            await _context.SaveChangesAsync();

            return game;
        }

        public Task<List<GameSession>> GetWaitingGamesAsync()
        {
            return _context.GameSessions
                .AsNoTracking()
                .Include(game => game.Players)
                .ThenInclude(gamePlayer => gamePlayer.Player)
                .Where(game => game.Status == GameStatus.WaitingForPlayers)
                .ToListAsync();
        }

        public Task<List<GameSession>> GetGamesByPlayerIdAsync(int playerId, PlayerGamesFilter filter)
        {
            IQueryable<GameSession> query = _context.GameSessions
                .AsNoTracking()
                .Include(game => game.Players)
                    .ThenInclude(gamePlayer => gamePlayer.Player)
                .Where(game => game.Players.Any(
                    gamePlayer => gamePlayer.PlayerId == playerId));

            query = filter switch
            {
                PlayerGamesFilter.Active =>
                    query.Where(game =>
                        game.Status == GameStatus.WaitingForGuesses ||
                        game.Status == GameStatus.WaitingForRollupGuesses),

                PlayerGamesFilter.NotStarted =>
                    query.Where(game =>
                        game.Status == GameStatus.WaitingForPlayers),

                PlayerGamesFilter.Completed =>
                    query.Where(game =>
                        game.Status == GameStatus.Completed),

                _ => query
            };

                return query
                    .OrderByDescending(game => game.CreatedAt)
                    .ToListAsync();
        }
    }
}