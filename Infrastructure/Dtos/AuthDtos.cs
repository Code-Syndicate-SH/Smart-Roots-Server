namespace Smart_Roots_Server.Infrastructure.Dtos
{
    public record RegisterRequest(string Email, string Password, string? Role);
    public record LoginRequest(string Email, string Password);
    public record LoginResponse(string AccessToken, string RefreshToken, object? User);
}
