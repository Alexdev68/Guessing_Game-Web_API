namespace GuessingGame.API.Models.Enums
{
    public enum GameStatus
    {
        WaitingForPlayers = 1,
        WaitingForGuesses = 2,
        WaitingForRollupGuesses = 3,
        Completed = 4,
        Cancelled = 5
    }
}