using GuessingGame.API.DTOs.Request;
using GuessingGame.API.Models;
using GuessingGame.API.Models.Enums;

namespace GuessingGame.API.Repositories.Interfaces
{
    public interface IGameRepository
    {
        Task<GameSession> GetByIdAsync(int gameId);
        Task AddAsync(GameSession game);
        void AddGamePlayer(GamePlayer gamePlayer);
        void AddGuess(PlayerGuess guess);
        Task<bool> GuessExistsAsync(int gamePlayerId, int roundNumber, bool isRollupGuess);

        Task<GameSession> SaveGame(CreateGameRequest request, GameConfig config, Player player, GameType selectedGame);
        Task SaveChangesAsync();
    }
}
