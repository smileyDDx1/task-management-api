using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TaskManagement.Application.Auth;
using TaskManagement.Application.Common.Exceptions;
using TaskManagement.Application.Common.Interfaces;
using TaskManagement.Domain.Entities;
using TaskManagement.Domain.Enums;

namespace TaskManagement.UnitTests.Auth;

public class AuthServiceTests
{
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IPasswordHasher _passwordHasher = Substitute.For<IPasswordHasher>();
    private readonly IJwtTokenGenerator _tokenGenerator = Substitute.For<IJwtTokenGenerator>();
    private readonly AuthService _sut;

    public AuthServiceTests()
    {
        _tokenGenerator.GenerateToken(Arg.Any<User>())
            .Returns(new AccessToken("fake-jwt", new DateTime(2030, 1, 1)));
        _passwordHasher.Hash(Arg.Any<string>()).Returns("hashed");

        _sut = new AuthService(_userRepository, _passwordHasher, _tokenGenerator, NullLogger<AuthService>.Instance);
    }

    [Fact]
    public async Task RegisterAsync_NewEmail_CreatesMemberAndReturnsToken()
    {
        var request = new RegisterRequest("Jane Doe", "jane@test.com", "Password1");

        var result = await _sut.RegisterAsync(request);

        await _userRepository.Received(1).AddAsync(
            Arg.Is<User>(u => u.Email == "jane@test.com" && u.Role == UserRole.Member && u.PasswordHash == "hashed"),
            Arg.Any<CancellationToken>());
        await _userRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        Assert.Equal("fake-jwt", result.AccessToken);
        Assert.Equal("Member", result.Role);
    }

    [Fact]
    public async Task RegisterAsync_TrimsAndLowercasesEmail()
    {
        var request = new RegisterRequest("  Jane Doe ", "  Jane@Test.COM ", "Password1");

        var result = await _sut.RegisterAsync(request);

        Assert.Equal("jane@test.com", result.Email);
        Assert.Equal("Jane Doe", result.FullName);
    }

    [Fact]
    public async Task RegisterAsync_EmailAlreadyUsed_ThrowsConflict()
    {
        _userRepository.EmailExistsAsync("jane@test.com", Arg.Any<CancellationToken>()).Returns(true);

        await Assert.ThrowsAsync<ConflictException>(() =>
            _sut.RegisterAsync(new RegisterRequest("Jane", "jane@test.com", "Password1")));

        await _userRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LoginAsync_ValidCredentials_ReturnsToken()
    {
        var user = new User { Id = 7, FullName = "Jane", Email = "jane@test.com", PasswordHash = "hash", Role = UserRole.Manager };
        _userRepository.GetByEmailAsync("jane@test.com", Arg.Any<CancellationToken>()).Returns(user);
        _passwordHasher.Verify("Password1", "hash").Returns(true);

        var result = await _sut.LoginAsync(new LoginRequest("jane@test.com", "Password1"));

        Assert.Equal(7, result.UserId);
        Assert.Equal("Manager", result.Role);
        Assert.Equal("fake-jwt", result.AccessToken);
    }

    [Fact]
    public async Task LoginAsync_UnknownEmail_ThrowsUnauthorized()
    {
        _userRepository.GetByEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((User?)null);

        await Assert.ThrowsAsync<UnauthorizedException>(() =>
            _sut.LoginAsync(new LoginRequest("nobody@test.com", "Password1")));
    }

    [Fact]
    public async Task LoginAsync_WrongPassword_ThrowsUnauthorized()
    {
        var user = new User { Id = 1, Email = "jane@test.com", PasswordHash = "hash" };
        _userRepository.GetByEmailAsync("jane@test.com", Arg.Any<CancellationToken>()).Returns(user);
        _passwordHasher.Verify(Arg.Any<string>(), Arg.Any<string>()).Returns(false);

        var ex = await Assert.ThrowsAsync<UnauthorizedException>(() =>
            _sut.LoginAsync(new LoginRequest("jane@test.com", "WrongPass1")));

        Assert.Equal("Invalid email or password.", ex.Message);
        _tokenGenerator.DidNotReceive().GenerateToken(Arg.Any<User>());
    }

    [Fact]
    public async Task LoginAsync_DeactivatedUser_ThrowsUnauthorized()
    {
        var user = new User { Id = 1, Email = "jane@test.com", PasswordHash = "hash", IsActive = false };
        _userRepository.GetByEmailAsync("jane@test.com", Arg.Any<CancellationToken>()).Returns(user);
        _passwordHasher.Verify(Arg.Any<string>(), Arg.Any<string>()).Returns(true);

        var ex = await Assert.ThrowsAsync<UnauthorizedException>(() =>
            _sut.LoginAsync(new LoginRequest("jane@test.com", "Password1")));

        Assert.Contains("deactivated", ex.Message);
    }
}
