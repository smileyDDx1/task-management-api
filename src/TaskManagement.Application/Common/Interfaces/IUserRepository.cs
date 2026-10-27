using TaskManagement.Application.Common.Models;
using TaskManagement.Application.Users;
using TaskManagement.Domain.Entities;

namespace TaskManagement.Application.Common.Interfaces;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken = default);
    Task<UserDto?> GetDtoByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<PagedResult<UserDto>> GetPagedAsync(UserQueryParameters query, CancellationToken cancellationToken = default);
    Task AddAsync(User user, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
