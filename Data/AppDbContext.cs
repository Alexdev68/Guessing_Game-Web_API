using GuessingGame.API.Models;
using Microsoft.EntityFrameworkCore;

namespace GuessingGame.API.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<Player> Players => Set<Player>();
        public DbSet<GameSession> GameSessions => Set<GameSession>();
        public DbSet<GamePlayer> GamePlayers => Set<GamePlayer>();
        public DbSet<PlayerGuess> PlayerGuesses => Set<PlayerGuess>();
        public DbSet<User> Users => Set<User>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Player>().HasIndex(x => x.Name).IsUnique();
            modelBuilder.Entity<Player>().Property(x => x.Balance).HasPrecision(18, 2);
            modelBuilder.Entity<GameSession>().Property(x => x.Multiplier).HasPrecision(18, 2);
            modelBuilder.Entity<GamePlayer>().Property(x => x.Stake).HasPrecision(18, 2);
            modelBuilder.Entity<GamePlayer>().Property(x => x.Winnings).HasPrecision(18, 2);

            modelBuilder.Entity<GamePlayer>()
                .HasIndex(x => new { x.GameSessionId, x.PlayerId })
                .IsUnique();

            modelBuilder.Entity<GamePlayer>()
                .HasOne(x => x.GameSession)
                .WithMany(x => x.Players)
                .HasForeignKey(x => x.GameSessionId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<GamePlayer>()
                .HasOne(x => x.Player)
                .WithMany(x => x.GameEntries)
                .HasForeignKey(x => x.PlayerId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<PlayerGuess>()
                .HasOne(x => x.GamePlayer)
                .WithMany(x => x.Guesses)
                .HasForeignKey(x => x.GamePlayerId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<PlayerGuess>()
                .HasIndex(x => new { x.GamePlayerId, x.RoundNumber, x.IsRollupGuess })
                .IsUnique();

            modelBuilder.Entity<User>()
                .HasIndex(user => user.Email)
                .IsUnique();

            modelBuilder.Entity<User>()
                .HasIndex(user => user.PlayerId)
                .IsUnique();

            modelBuilder.Entity<User>()
                .HasOne(user => user.Player)
                .WithOne(player => player.User)
                .HasForeignKey<User>(user => user.PlayerId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<User>()
                .HasIndex(user => user.ApiKeyHash)
                .IsUnique();
        }
    }
}