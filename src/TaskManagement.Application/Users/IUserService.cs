using TaskManagement.Application.Common.Models;

namespace TaskManagement.Application.Users;

public interface IUserService
{
    Task<PagedResult<UserDto>> GetUsersAsync(UserQueryParameters query, CancellationToken cancellationToken = default);
    Task<UserDto> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<UserDto> GetCurrentUserAsync(CancellationToken cancellationToken = default);
    Task<UserDto> UpdateRoleAsync(int id, UpdateUserRoleRequest request, CancellationToken cancellationToken = default);
    Task DeactivateAsync(int id, CancellationToken cancellationToken = default);
}
