using GuessingGame.API.DTOs.Response;
using GuessingGame.API.Models;

namespace GuessingGame.API.Services
{
    internal static class GuessParser
    {
        public static ApiResponse<string> ParseGuesses(string input, GameConfig config)
        {
            List<string> guesses = input.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

            if (guesses.Count != config.GuessLength)
            {
                return Fail($"Enter exactly {config.GuessLength} values.");
            }

            if (guesses.Any(string.IsNullOrWhiteSpace))
            {
                return Fail("Guess values cannot be empty.");
            }

            if (!config.AllowDuplicates && guesses.Distinct(StringComparer.OrdinalIgnoreCase).Count() != guesses.Count)
            {
                return Fail("Duplicates are not allowed.");
            }

            foreach (string guess in guesses)
            {
                if (config.AllowAlphanumeric)
                {
                    bool isNumber = int.TryParse(guess, out int number);

                    if (isNumber)
                    {
                        if (number < config.MinValue || number > config.MaxValue)
                        {
                            return Fail($"{number} must be between " + $"{config.MinValue} and {config.MaxValue}.");
                        }
                    }
                    else if (guess.Length != 1 || !char.IsLetter(guess[0]))
                    {
                        return Fail("Only numbers or single letters are allowed.");
                    }
                }
                else
                {
                    if (!int.TryParse(guess, out int number))
                    {
                        return Fail($"'{guess}' is not a valid number.");
                    }

                    if (number < config.MinValue || number > config.MaxValue)
                    {
                        return Fail($"{number} must be between " + $"{config.MinValue} and {config.MaxValue}.");
                    }
                }
            }

            return new ApiResponse<string>
            {
                Success = true,
                Message = "Guesses are valid.",
                Data = string.Join(",", guesses)
            };
        }

        private static ApiResponse<string> Fail(string message)
        {
            return new ApiResponse<string>
            {
                Success = false,
                Message = message,
                Data = null
            };
        }
    }
}