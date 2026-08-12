using GuessingGame.API.Data;
using GuessingGame.API.Models;
using GuessingGame.API.Repositories.Interfaces;
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
    }
}