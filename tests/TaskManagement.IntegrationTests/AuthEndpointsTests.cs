using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using TaskManagement.Application.Auth;
using TaskManagement.Application.Users;
using TaskManagement.Domain.Enums;
using TaskManagement.IntegrationTests.Infrastructure;

namespace TaskManagement.IntegrationTests;

public class AuthEndpointsTests : ApiTestBase
{
    public AuthEndpointsTests(CustomWebApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task Register_NewUser_Returns201WithTokenAndMemberRole()
    {
        var response = await CreateClient().PostAsJsonAsync("/api/auth/register",
            new RegisterRequest("Jane Doe", UniqueEmail(), DefaultPassword));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>(TestJson.Options);
        Assert.False(string.IsNullOrEmpty(auth!.AccessToken));
        Assert.Equal("Member", auth.Role);
    }

    [Fact]
    public async Task Register_DuplicateEmail_Returns409()
    {
        var client = CreateClient();
        var email = UniqueEmail();
        await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest("Jane", email, DefaultPassword));

        var response = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest("Jane again", email, DefaultPassword));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Register_InvalidBody_Returns400WithValidationErrors()
    {
        var response = await CreateClient().PostAsJsonAsync("/api/auth/register",
            new RegisterRequest("", "not-an-email", "weak"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var errors = json.RootElement.GetProperty("errors");
        Assert.True(errors.TryGetProperty("Email", out _));
        Assert.True(errors.TryGetProperty("Password", out _));
        Assert.True(errors.TryGetProperty("FullName", out _));
    }

    [Fact]
    public async Task Login_WrongPassword_Returns401()
    {
        var response = await CreateClient().PostAsJsonAsync("/api/auth/login",
            new LoginRequest(CustomWebApplicationFactory.AdminEmail, "WrongPassword1"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_SeededAdmin_ReturnsAdminRole()
    {
        var response = await CreateClient().PostAsJsonAsync("/api/auth/login",
            new LoginRequest(CustomWebApplicationFactory.AdminEmail, CustomWebApplicationFactory.AdminPassword));

        response.EnsureSuccessStatusCode();
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>(TestJson.Options);
        Assert.Equal("Admin", auth!.Role);
    }

    [Theory]
    [InlineData("/api/projects")]
    [InlineData("/api/tasks")]
    [InlineData("/api/users/me")]
    public async Task ProtectedEndpoints_WithoutToken_Return401(string url)
    {
        var response = await CreateClient().GetAsync(url);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithGarbageToken_Returns401()
    {
        var response = await CreateClient("this.is.not-a-jwt").GetAsync("/api/users/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetMe_ReturnsLoggedInUser()
    {
        var user = await CreateUserAsync();

        var me = await user.Client.GetFromJsonAsync<UserDto>("/api/users/me", TestJson.Options);

        Assert.Equal(user.Id, me!.Id);
        Assert.Equal(user.Email, me.Email);
        Assert.Equal(UserRole.Member, me.Role);
    }
}
