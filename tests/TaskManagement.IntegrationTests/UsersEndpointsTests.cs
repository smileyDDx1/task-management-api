using System.Net;
using System.Net.Http.Json;
using TaskManagement.Application.Auth;
using TaskManagement.Application.Common.Models;
using TaskManagement.Application.Users;
using TaskManagement.Domain.Enums;
using TaskManagement.IntegrationTests.Infrastructure;

namespace TaskManagement.IntegrationTests;

public class UsersEndpointsTests : ApiTestBase
{
    public UsersEndpointsTests(CustomWebApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task GetUsers_AsMember_Returns403()
    {
        var member = await CreateUserAsync();

        var response = await member.Client.GetAsync("/api/users");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetUsers_AsManager_ReturnsPagedResult()
    {
        var manager = await CreateUserAsync(UserRole.Manager);

        var page = await manager.Client.GetFromJsonAsync<PagedResult<UserDto>>("/api/users?page=1&pageSize=2", TestJson.Options);

        Assert.NotNull(page);
        Assert.True(page.Items.Count <= 2);
        Assert.True(page.TotalCount >= 2);
    }

    [Fact]
    public async Task UpdateRole_AsAdmin_PromotesUser()
    {
        var admin = await CreateAdminClientAsync();
        var user = await CreateUserAsync();

        var response = await admin.PutAsJsonAsync($"/api/users/{user.Id}/role", new UpdateUserRoleRequest(UserRole.Manager), TestJson.Options);

        response.EnsureSuccessStatusCode();
        var updated = await response.Content.ReadFromJsonAsync<UserDto>(TestJson.Options);
        Assert.Equal(UserRole.Manager, updated!.Role);
    }

    [Fact]
    public async Task UpdateRole_AsManager_Returns403()
    {
        var manager = await CreateUserAsync(UserRole.Manager);
        var user = await CreateUserAsync();

        var response = await manager.Client.PutAsJsonAsync($"/api/users/{user.Id}/role", new UpdateUserRoleRequest(UserRole.Admin), TestJson.Options);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Deactivate_AsAdmin_UserCanNoLongerLogIn()
    {
        var admin = await CreateAdminClientAsync();
        var user = await CreateUserAsync();

        var response = await admin.DeleteAsync($"/api/users/{user.Id}");
        var login = await CreateClient().PostAsJsonAsync("/api/auth/login", new LoginRequest(user.Email, DefaultPassword));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
    }

    [Fact]
    public async Task GetById_UnknownUser_Returns404()
    {
        var admin = await CreateAdminClientAsync();

        var response = await admin.GetAsync("/api/users/999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
