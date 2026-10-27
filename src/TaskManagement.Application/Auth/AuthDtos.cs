namespace TaskManagement.Application.Auth;

public record RegisterRequest(string FullName, string Email, string Password);

public record LoginRequest(string Email, string Password);

public record AuthResponse(
    int UserId,
    string FullName,
    string Email,
    string Role,
    string AccessToken,
    DateTime ExpiresAt);
