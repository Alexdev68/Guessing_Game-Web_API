using System.Text.Json.Serialization;

namespace GuessingGame.API.DTOs.Response
{
    public class ApiResponse
    {
        [JsonPropertyOrder(1)]
        public bool Success { get; set; }

        [JsonPropertyOrder(2)]
        public string Message { get; set; } = string.Empty;
    }

    public sealed class ApiResponse<T> : ApiResponse
    {
        [JsonPropertyOrder(3)]
        public T? Data { get; set; }
    }
}