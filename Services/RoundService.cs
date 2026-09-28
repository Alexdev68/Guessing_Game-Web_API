using GuessingGame.API.DTOs.Request;
using GuessingGame.API.DTOs.Response;
using GuessingGame.API.Models;
using GuessingGame.API.Models.Enums;
using GuessingGame.API.Repositories.Interfaces;
using GuessingGame.API.Services.Interfaces;
using GuessingGame.API.Caching;
using Microsoft.Extensions.Caching.Memory;

namespace GuessingGame.API.Services
{
    public class RoundService : IRoundService
    {
        private readonly IGameRepository _games;
        private readonly ILogger<RoundService> _logger;
        private readonly IMemoryCache _cache;
        public RoundService(IGameRepository games, ILogger<RoundService> logger, IMemoryCache cache)
        {
            _games = games;
            _logger = logger;
            _cache = cache;
        }

        public Task<ApiResponse<SubmitGuessResponse>> SubmitGuessAsync(int gameId, int playerId, SubmitGuessRequest request)
        {
            return SaveAndEvaluateAsync(gameId, request, playerId, false);
        }

        public Task<ApiResponse<SubmitGuessResponse>> SubmitRollupGuessAsync(int gameId, int playerId, SubmitGuessRequest request)
        {
            return SaveAndEvaluateAsync(gameId, request, playerId, true);
        }

        private async Task<ApiResponse<SubmitGuessResponse>> SaveAndEvaluateAsync(int gameId, SubmitGuessRequest request, int playerId, bool isRollup)
        {
            GameSession? game = await _games.GetByIdAsync(gameId);

            if (game is null)
            {
                _logger.LogWarning("Game {GameId} submission rejected because game was not found.", gameId);
                return Fail("Game not found.");
            }

            GameConfig config = GameSettings.GetConfig(game.GameType);

            GameStatus required = isRollup ? GameStatus.WaitingForRollupGuesses : GameStatus.WaitingForGuesses;

            if (game.Status != required)
            {
                _logger.LogWarning("Game {GameId} guess submission rejected because game has status {GameStatus}.", gameId, game.Status);
                return Fail($"Guesses cannot be submitted while status is {game.Status}.");
            }

            GamePlayer? entry = game.Players.FirstOrDefault(x => x.PlayerId == playerId);
            if (entry is null)
            {
                _logger.LogWarning("Game {GameId} guess submission rejected because player with ID {PlayerId} is not part of the game.", gameId, playerId);
                return Fail("This player is not part of the game.");
            }

            if ((!isRollup && entry.Status != PlayerStatus.Active) || (isRollup && entry.Status != PlayerStatus.InRollup))
            {
                _logger.LogWarning("Game {GameId} guess submission rejected because player with ID {PlayerId} is not active in this stage with status {PlayerStatus}.", gameId, playerId, entry.Status);
                return Fail("This player is not active in this stage.");
            }

            ApiResponse<string> parseResult = GuessParser.ParseGuesses(request.Guesses, config);

            int round = isRollup ? game.RollupRound : game.CurrentRound;

            if (!parseResult.Success)
            {
                _logger.LogWarning("Game {GameId} guess submission was rejected by validation for RoundNumber {RoundNumber}.", gameId, round);
                return Fail(parseResult.Message);
            }

            if (await _games.GuessExistsAsync(entry.Id, round, isRollup))
            {
                _logger.LogWarning("Game {GameId} duplicate guess submission was rejected for RoundNumber {RoundNumber}.", gameId, round);
                return Fail("This player has already submitted for this round.");
            }

            var guess = new PlayerGuess
            {
                GamePlayerId = entry.Id,
                RoundNumber = round,
                GuessValues = parseResult.Data!,
                IsRollupGuess = isRollup
            };

            _games.AddGuess(guess);
            await _games.SaveChangesAsync();

            _cache.Remove(CacheKeys.Game(gameId));

            _logger.LogInformation("Game {GameId} guess submitted successfully for RoundNumber {RoundNumber}.", gameId, round);

            game = (await _games.GetByIdAsync(gameId))!;
            List<GamePlayer> requiredPlayers = game.Players
                .Where(x => isRollup
                    ? x.Status == PlayerStatus.InRollup
                    : x.Status == PlayerStatus.Active)
                .ToList();

            int submitted = requiredPlayers.Count(x => x.Guesses.Any(g => g.RoundNumber == round && g.IsRollupGuess == isRollup));

            if (submitted < requiredPlayers.Count)
            {
                _logger.LogDebug("Game {GameId} round {RoundNumber} is waiting for more guesses: {SubmittedCount}/{RequiredCount}", gameId, round, submitted, requiredPlayers.Count);

                return Ok("Guess saved. Waiting for the other active players.", new SubmitGuessResponse
                {
                    GuessId = guess.Id,
                    GameId = gameId,
                    PlayerId = playerId,
                    SubmittedRound = round,
                    IsRollupGuess = isRollup,
                    RoundEvaluated = false,
                    CurrentRound = game.CurrentRound,
                    GameStatus = game.Status,
                    RequiredPlayers = requiredPlayers.Count,
                    SubmittedPlayers = submitted
                });
            }

            _logger.LogInformation("Game {GameId} evaluating round {RoundNumber} with {ParticipantCount} participants ", gameId, round, requiredPlayers.Count);
            RoundEvaluationResult evaluation = isRollup ? EvaluateRollup(game, round, _logger) : EvaluateNormalRound(game, round, _logger);

            await _games.SaveChangesAsync();

            RemoveGameRelatedCaches(game);

            _logger.LogInformation("Game {GameId} round {RoundNumber} evaluation completed", gameId, round);

            return Ok(evaluation.Message, new SubmitGuessResponse
            {
                GuessId = guess.Id,
                GameId = gameId,
                PlayerId = playerId,
                SubmittedRound = round,
                IsRollupGuess = isRollup,
                RoundEvaluated = true,
                CurrentRound = game.CurrentRound,
                GameStatus = game.Status,
                RequiredPlayers = requiredPlayers.Count,
                SubmittedPlayers = submitted,
                FinalWinnerPlayerId = game.FinalWinnerPlayerId,
                RoundWinnerPlayerIds = evaluation.WinnerPlayerIds
            });
        }

        private static RoundEvaluationResult EvaluateNormalRound(GameSession game, int round, ILogger _logger)
        {
            GameConfig config = GameSettings.GetConfig(game.GameType);

            List<GamePlayer> activePlayers = game.Players
                .Where(player =>
                    player.Status == PlayerStatus.Active).ToList();

            List<GamePlayer> roundWinners = EvaluateParticipants(
                game,
                activePlayers,
                round,
                isRollup: false,
                _logger: _logger);

            bool activePlayersRemain = game.Players.Any(player => player.Status == PlayerStatus.Active);

            bool attemptsRemain = game.CurrentRound < game.Attempts;

            if (activePlayersRemain && attemptsRemain)
            {
                game.CurrentRound++;
                game.Status = GameStatus.WaitingForGuesses;

                return Result($"Round {round} completed. Round {game.CurrentRound} has started.", roundWinners);
            }

            List<GamePlayer> successfulPlayers = game.Players.Where(player => player.Status == PlayerStatus.Won).ToList();

            if (successfulPlayers.Count == 0)
            {
                _logger.LogInformation("Game {GameId} completed without a winner", game.Id);

                CompleteWithoutWinner(game);

                return Result("The game ended without a winner.", successfulPlayers);
            }

            if (successfulPlayers.Count == 1)
            {
                _logger.LogInformation("Game {GameId} completed with a single winner: {WinnerPlayerId}", game.Id, successfulPlayers[0].PlayerId);
                CompleteWithWinner(game, successfulPlayers[0]);

                return Result("The game completed with one winner.", successfulPlayers);
            }

            if (game.AllowRollup)
            {
                foreach (GamePlayer player in game.Players)
                {
                    player.Status = successfulPlayers.Contains(player)
                        ? PlayerStatus.InRollup
                        : PlayerStatus.Lost;
                }

                _logger.LogInformation("Game {GameId} has entered rollup with {WinnerCount} players", game.Id, successfulPlayers.Count);

                game.RollupRound = 1;
                game.Status = GameStatus.WaitingForRollupGuesses;
                game.WinningNumbers = string.Join(", ", RandomGenerator.Generate(config));

                return Result("Multiple players succeeded. Rollup round 1 has started.", successfulPlayers);
            }

            CompleteWithMultipleWinners(game, successfulPlayers);

            return Result("The game completed with multiple winners.", successfulPlayers);
        }

        private static RoundEvaluationResult EvaluateRollup(GameSession game, int round, ILogger _logger)
        {
            GameConfig config = GameSettings.GetConfig(game.GameType);

            List<GamePlayer> rollupPlayers = game.Players
                .Where(player => player.Status == PlayerStatus.InRollup).ToList();

            List<GamePlayer> rollupWinners = EvaluateParticipants(
                    game,
                    rollupPlayers,
                    round,
                    isRollup: true,
                    _logger: _logger);

            if (rollupWinners.Count == 1)
            {
                GamePlayer finalWinner = rollupWinners[0];

                foreach (GamePlayer player in rollupPlayers)
                {
                    if (player.Id != finalWinner.Id)
                    {
                        player.Status = PlayerStatus.LostInRollup;
                    }
                }

                _logger.LogInformation("Game {GameId} completed with a final winner: Player {PlayerId}", game.Id, finalWinner.PlayerId);

                CompleteWithWinner(game, finalWinner);

                return Result("Rollup completed with one final winner.", rollupWinners);
            }

            if (rollupWinners.Count == 0)
            {
                foreach (GamePlayer player in rollupPlayers)
                {
                    player.Status =
                        PlayerStatus.LostInRollup;
                }

                _logger.LogInformation("Rollup completed with no winners. Game {GameId} ended without a winner.", game.Id);

                CompleteWithoutWinner(game);

                return Result("Nobody won the rollup. The game ended without a winner.", rollupWinners);
            }

            if (rollupWinners.Count > 1)
            {
                game.WinningNumbers = string.Join(", ", RandomGenerator.Generate(config));
            }

            foreach (GamePlayer player in rollupPlayers)
            {
                player.Status = rollupWinners.Contains(player)
                    ? PlayerStatus.InRollup
                    : PlayerStatus.LostInRollup;
            }

            _logger.LogInformation("Game {GameId} continues to Rollup round {RollupRound} with {WinnerCount} players", game.Id, game.RollupRound + 1, rollupWinners.Count);
            game.RollupRound++;
            game.Status = GameStatus.WaitingForRollupGuesses;

            return Result($"Multiple rollup winners. Rollup round {game.RollupRound} has started.", rollupWinners);
        }

        private static List<GamePlayer> EvaluateParticipants(GameSession game, List<GamePlayer> participants, int round, bool isRollup, ILogger _logger)
        {
            GameConfig config = GameSettings.GetConfig(game.GameType);
            List<string> winningNumbers = game.WinningNumbers.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(value => value.Trim())
            .ToList();

            var winners = new List<GamePlayer>();

            foreach (GamePlayer participant in participants)
            {
                Validation.EvaluatePlayerGuess(winningNumbers, participant, config, round, isRollup);

                PlayerGuess? currentGuess = participant.Guesses.FirstOrDefault(guess =>
                        guess.RoundNumber == round &&
                        guess.IsRollupGuess == isRollup);

                if (currentGuess?.IsCorrect != true)
                    continue;

                _logger.LogInformation("Player {playerId} guessed correctly in Game {GameId}, round {RoundNumber}", participant.PlayerId, game.Id, round);

                if (!isRollup)
                {
                    participant.Status = PlayerStatus.Won;

                    participant.WinningRound = round;
                }

                winners.Add(participant);
            }

            return winners;
        }

        private static void CompleteWithMultipleWinners(GameSession game, List<GamePlayer> winners)
        {
            foreach (GamePlayer winner in winners)
            {
                winner.Status = PlayerStatus.Won;
                winner.Winnings = winner.Stake * game.Multiplier;
                winner.Player.Balance += winner.Winnings;
                winner.Player.TotalWins++;
            }

            game.FinalWinnerPlayerId = null;

            Finish(game, winners.Select(winner => winner.PlayerId).ToList());
        }

        private static void CompleteWithWinner(GameSession game, GamePlayer winner)
        {
            winner.Status = PlayerStatus.FinalWinner;
            winner.Winnings = game.Players.Sum(player => player.Stake) * game.Multiplier;

            winner.Player.Balance += winner.Winnings;
            winner.Player.TotalWins++;

            game.FinalWinnerPlayerId = winner.PlayerId;

            Finish(game, new List<int> { winner.PlayerId });
        }

        private static void CompleteWithoutWinner(GameSession game)
        {
            game.FinalWinnerPlayerId = null;
            Finish(game, new List<int>());
        }

        private static void Finish(GameSession game, List<int> winnerPlayerIds)
        {
            game.Status = GameStatus.Completed;
            game.CompletedAt = DateTime.UtcNow;

            foreach (GamePlayer entry in game.Players)
            {
                entry.Player.GamesPlayed++;
                entry.Player.TotalScore += entry.Score;
                entry.Player.BestScore = Math.Max(entry.Player.BestScore, entry.Score);

                entry.Player.LastSeen = DateTime.UtcNow;

                if (!winnerPlayerIds.Contains(entry.PlayerId) && entry.Status != PlayerStatus.LostInRollup)
                {
                    entry.Status = PlayerStatus.Lost;
                }
            }
        }

        private void RemoveGameRelatedCaches(GameSession game)
        {
            _cache.Remove(CacheKeys.Game(game.Id));

            _cache.Remove(CacheKeys.AvailableGames);

            foreach (int playerId in game.Players.Select(x => x.PlayerId).Distinct())
            {
                foreach (PlayerGamesFilter filter in Enum.GetValues<PlayerGamesFilter>())
                {
                    _cache.Remove(CacheKeys.PlayerGames(playerId, filter));
                }
            }
        }

        private static RoundEvaluationResult Result(string message, IEnumerable<GamePlayer> winners) =>
        new() { Message = message, WinnerPlayerIds = winners.Select(x => x.PlayerId).ToList() };

        private static ApiResponse<SubmitGuessResponse> Ok(string message, SubmitGuessResponse data) =>
            new() { Success = true, Message = message, Data = data };

        private static ApiResponse<SubmitGuessResponse> Fail(string message) =>
            new() { Success = false, Message = message };
    }
}