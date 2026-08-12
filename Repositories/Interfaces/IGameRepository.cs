using GuessingGame.API.Models;

namespace GuessingGame.API.Repositories.Interfaces
{
    public interface IGameRepository
    {
        Task<GameSession> GetByIdAsync(int gameId);
        Task AddAsync(GameSession game);
        void AddGamePlayer(GamePlayer gamePlayer);
        void AddGuess(PlayerGuess guess);
        Task<bool> GuessExistsAsync(int gamePlayerId, int roundNumber, bool isRollupGuess);
        Task SaveChangesAsync();
    }
}
