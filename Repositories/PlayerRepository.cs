using GuessingGame.API.Data;
using GuessingGame.API.Models;
using GuessingGame.API.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace GuessingGame.API.Repositories
{
    public class PlayerRepository : IPlayerRepository
    {
        private readonly AppDbContext _context;
        public PlayerRepository(AppDbContext context) => _context = context;

        public async Task AddAsync(Player player) => await _context.Players.AddAsync(player);

        public Task<Player?> GetByIdAsync(int playerId) =>
            _context.Players.FirstOrDefaultAsync(x => x.Id == playerId);

        public Task<Player?> GetByNameAsync(string name)
        {
            string normalized = name.Trim().ToLower();
            return _context.Players.FirstOrDefaultAsync(x => x.Name.ToLower() == normalized);
        }

        public Task SaveChangesAsync() => _context.SaveChangesAsync();
    }
}
