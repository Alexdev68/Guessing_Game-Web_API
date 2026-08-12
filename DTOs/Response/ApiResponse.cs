namespace GuessingGame.API.DTOs.Response
{
    public class ApiResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    public sealed class ApiResponse<T> : ApiResponse
    {
        public T? Data { get; set; }
    }
}