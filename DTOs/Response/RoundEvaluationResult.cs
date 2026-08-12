namespace GuessingGame.API.DTOs.Response
{
    public class RoundEvaluationResult
    {
        public string Message { get; set; } = string.Empty;
        public List<int> WinnerPlayerIds { get; set; } = new();
    }
}