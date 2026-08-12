using GuessingGame.API.Models;
using System.Collections.Generic;

namespace GuessingGame.API.Services
{
    internal class Validation
    {
        public static void EvaluatePlayerGuess(List<string> winningNumbers, GamePlayer player, GameConfig config, int currentRound, bool isRollup)
        {
            int matches = 0;
            var sourceCounts = new Dictionary<string, int>(winningNumbers.Count);

            if (player.Guesses.Count == 0)
                return;

            PlayerGuess? currentGuess = player.Guesses.FirstOrDefault(g => g.RoundNumber == currentRound && g.IsRollupGuess == isRollup);

            if (currentGuess is null)
                return;

            string lastGuess = currentGuess.GuessValues;

            List<string> guesses = lastGuess.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(g => g.Trim()).ToList();

            foreach (string item in winningNumbers)
            {
                if (sourceCounts.TryGetValue(item, out int count))
                {
                    sourceCounts[item] = count + 1;
                }
                else
                {
                    sourceCounts[item] = 1;
                }
            }

            foreach (string guess in guesses)
            {
                string key = config.AllowAlphanumeric ? guess.ToUpperInvariant() : guess;

                if (sourceCounts.TryGetValue(key, out int count) && count > 0)
                {
                    matches++;
                    sourceCounts[key] = count - 1;
                }
            }

            currentGuess.MatchCount = matches;
            currentGuess.IsCorrect = matches == config.GuessLength;

            player.Score = (matches * 100) / config.GuessLength;
        }
    }
}