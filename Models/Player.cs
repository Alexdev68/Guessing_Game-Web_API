using System.Text.Json.Serialization;

namespace GuessingGame.API.Models
{
    public class Player
    {
        public int Id { get; set; }
        public string Name { get; set; }

        public decimal Balance { get; set; } = 5000;

        public int GamesPlayed { get; set; }

        public int TotalWins { get; set; }

        public int BestScore { get; set; }

        public int TotalScore { get; set; }

        public double AverageScore => GamesPlayed == 0 ? 0 : (double)TotalScore / GamesPlayed;

        public DateTime FirstSeen { get; set; } = DateTime.UtcNow;

        public DateTime LastSeen { get; set; } = DateTime.UtcNow;

        public ICollection<GamePlayer> GameEntries { get; set; } = new List<GamePlayer>();
    }
}