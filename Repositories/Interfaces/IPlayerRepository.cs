using GuessingGame.API.Models;

namespace GuessingGame.API.Repositories.Interfaces
{
    public interface IPlayerRepository
    {
        Task<Player?> GetByIdAsync(int playerId);
        Task<Player?> GetByNameAsync(string name);
        Task AddAsync(Player player);
        Task SaveChangesAsync();
    }
}