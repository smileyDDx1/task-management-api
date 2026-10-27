using TaskManagement.Domain.Entities;

namespace TaskManagement.Application.Common.Interfaces;

public record AccessToken(string Token, DateTime ExpiresAt);

public interface IJwtTokenGenerator
{
    AccessToken GenerateToken(User user);
}
