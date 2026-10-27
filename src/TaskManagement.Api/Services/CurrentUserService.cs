using System.Security.Claims;
using TaskManagement.Application.Common.Exceptions;
using TaskManagement.Application.Common.Interfaces;
using TaskManagement.Domain.Enums;

namespace TaskManagement.Api.Services;

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    private ClaimsPrincipal? User => _httpContextAccessor.HttpContext?.User;

    public int UserId
    {
        get
        {
            var sub = User?.FindFirstValue("sub");
            return int.TryParse(sub, out var id)
                ? id
                : throw new UnauthorizedException("User id claim is missing from the token.");
        }
    }

    public UserRole Role
    {
        get
        {
            var role = User?.FindFirstValue("role");
            // fall back to the lowest role if the claim is missing or invalid
            return Enum.TryParse<UserRole>(role, out var parsed) ? parsed : UserRole.Member;
        }
    }
}
