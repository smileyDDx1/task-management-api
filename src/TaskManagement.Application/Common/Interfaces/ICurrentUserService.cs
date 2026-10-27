using TaskManagement.Domain.Enums;

namespace TaskManagement.Application.Common.Interfaces;

// implemented in the Api layer, reads the logged in user from the JWT claims
public interface ICurrentUserService
{
    int UserId { get; }
    UserRole Role { get; }
}
