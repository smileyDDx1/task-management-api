using Microsoft.Extensions.Logging;
using TaskManagement.Application.Common.Exceptions;
using TaskManagement.Application.Common.Interfaces;
using TaskManagement.Application.Common.Models;

namespace TaskManagement.Application.Users;

public class UserService : IUserService
{
    private readonly IUserRepository _userRepository;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<UserService> _logger;

    public UserService(IUserRepository userRepository, ICurrentUserService currentUser, ILogger<UserService> logger)
    {
        _userRepository = userRepository;
        _currentUser = currentUser;
        _logger = logger;
    }

    public Task<PagedResult<UserDto>> GetUsersAsync(UserQueryParameters query, CancellationToken cancellationToken = default)
    {
        return _userRepository.GetPagedAsync(query, cancellationToken);
    }

    public async Task<UserDto> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetDtoByIdAsync(id, cancellationToken);
        return user ?? throw new NotFoundException("User", id);
    }

    public Task<UserDto> GetCurrentUserAsync(CancellationToken cancellationToken = default)
    {
        return GetByIdAsync(_currentUser.UserId, cancellationToken);
    }

    public async Task<UserDto> UpdateRoleAsync(int id, UpdateUserRoleRequest request, CancellationToken cancellationToken = default)
    {
        // stops an admin from accidentally removing their own admin access
        if (id == _currentUser.UserId)
        {
            throw new BadRequestException("You cannot change your own role.");
        }

        var user = await _userRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException("User", id);

        var oldRole = user.Role;
        user.Role = request.Role;
        await _userRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("User {UserId} role changed from {OldRole} to {NewRole} by {AdminId}",
            user.Id, oldRole, user.Role, _currentUser.UserId);

        return await GetByIdAsync(id, cancellationToken);
    }

    public async Task DeactivateAsync(int id, CancellationToken cancellationToken = default)
    {
        if (id == _currentUser.UserId)
        {
            throw new BadRequestException("You cannot deactivate your own account.");
        }

        var user = await _userRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException("User", id);

        if (!user.IsActive)
        {
            return;
        }

        user.IsActive = false;
        await _userRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("User {UserId} deactivated by {AdminId}", user.Id, _currentUser.UserId);
    }
}
