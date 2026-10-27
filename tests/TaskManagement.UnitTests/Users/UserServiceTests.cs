using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TaskManagement.Application.Common.Exceptions;
using TaskManagement.Application.Common.Interfaces;
using TaskManagement.Application.Users;
using TaskManagement.Domain.Entities;
using TaskManagement.Domain.Enums;
using TaskManagement.UnitTests.Helpers;

namespace TaskManagement.UnitTests.Users;

public class UserServiceTests
{
    private const int AdminId = 1;

    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly UserService _sut;

    public UserServiceTests()
    {
        _sut = new UserService(_userRepository, TestUsers.CurrentUser(AdminId, UserRole.Admin), NullLogger<UserService>.Instance);
    }

    [Fact]
    public async Task GetByIdAsync_UserDoesNotExist_ThrowsNotFound()
    {
        _userRepository.GetDtoByIdAsync(99, Arg.Any<CancellationToken>()).Returns((UserDto?)null);

        await Assert.ThrowsAsync<NotFoundException>(() => _sut.GetByIdAsync(99));
    }

    [Fact]
    public async Task GetCurrentUserAsync_UsesIdFromToken()
    {
        var dto = new UserDto(AdminId, "Admin", "admin@test.com", UserRole.Admin, true, DateTime.UtcNow);
        _userRepository.GetDtoByIdAsync(AdminId, Arg.Any<CancellationToken>()).Returns(dto);

        var result = await _sut.GetCurrentUserAsync();

        Assert.Same(dto, result);
    }

    [Fact]
    public async Task UpdateRoleAsync_ChangesRoleAndSaves()
    {
        var user = new User { Id = 5, Role = UserRole.Member };
        _userRepository.GetByIdAsync(5, Arg.Any<CancellationToken>()).Returns(user);
        _userRepository.GetDtoByIdAsync(5, Arg.Any<CancellationToken>())
            .Returns(new UserDto(5, "Bob", "bob@test.com", UserRole.Manager, true, DateTime.UtcNow));

        var result = await _sut.UpdateRoleAsync(5, new UpdateUserRoleRequest(UserRole.Manager));

        Assert.Equal(UserRole.Manager, user.Role);
        Assert.Equal(UserRole.Manager, result.Role);
        await _userRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateRoleAsync_OwnAccount_ThrowsBadRequest()
    {
        await Assert.ThrowsAsync<BadRequestException>(() =>
            _sut.UpdateRoleAsync(AdminId, new UpdateUserRoleRequest(UserRole.Member)));
    }

    [Fact]
    public async Task UpdateRoleAsync_UserDoesNotExist_ThrowsNotFound()
    {
        _userRepository.GetByIdAsync(42, Arg.Any<CancellationToken>()).Returns((User?)null);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            _sut.UpdateRoleAsync(42, new UpdateUserRoleRequest(UserRole.Manager)));
    }

    [Fact]
    public async Task DeactivateAsync_ActiveUser_SetsIsActiveFalse()
    {
        var user = new User { Id = 5, IsActive = true };
        _userRepository.GetByIdAsync(5, Arg.Any<CancellationToken>()).Returns(user);

        await _sut.DeactivateAsync(5);

        Assert.False(user.IsActive);
        await _userRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeactivateAsync_AlreadyInactive_DoesNotSaveAgain()
    {
        _userRepository.GetByIdAsync(5, Arg.Any<CancellationToken>()).Returns(new User { Id = 5, IsActive = false });

        await _sut.DeactivateAsync(5);

        await _userRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeactivateAsync_OwnAccount_ThrowsBadRequest()
    {
        await Assert.ThrowsAsync<BadRequestException>(() => _sut.DeactivateAsync(AdminId));
    }
}
