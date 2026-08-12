namespace GuessingGame.API.Models.Enums
{
    public enum GameStatus
    {
        WaitingForGuesses = 1,
        WaitingForRollupGuesses = 2,
        Completed = 3,
        Cancelled = 4
    }
}