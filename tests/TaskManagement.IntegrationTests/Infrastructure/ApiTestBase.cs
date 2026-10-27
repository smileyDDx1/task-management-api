using System.Net.Http.Headers;
using System.Net.Http.Json;
using TaskManagement.Application.Auth;
using TaskManagement.Application.Projects;
using TaskManagement.Application.Tasks;
using TaskManagement.Application.Users;
using TaskManagement.Domain.Enums;

namespace TaskManagement.IntegrationTests.Infrastructure;

[CollectionDefinition(Name)]
public class ApiCollection : ICollectionFixture<CustomWebApplicationFactory>
{
    public const string Name = "Api";
}

public record TestUser(int Id, string Email, HttpClient Client);

// all integration tests share one api + database, so every test creates its own users/projects
// with unique emails instead of relying on a clean database
[Collection(ApiCollection.Name)]
public abstract class ApiTestBase
{
    protected const string DefaultPassword = "Password1";

    protected readonly CustomWebApplicationFactory Factory;

    protected ApiTestBase(CustomWebApplicationFactory factory)
    {
        Factory = factory;
    }

    protected HttpClient CreateClient(string? token = null)
    {
        var client = Factory.CreateClient();
        if (token is not null)
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return client;
    }

    protected async Task<HttpClient> CreateAdminClientAsync()
    {
        return CreateClient(await Factory.GetAdminTokenAsync());
    }

    protected static string UniqueEmail() => $"user-{Guid.NewGuid():N}@test.local";

    protected async Task<TestUser> CreateUserAsync(UserRole role = UserRole.Member)
    {
        var anonymous = CreateClient();
        var email = UniqueEmail();

        var register = await anonymous.PostAsJsonAsync("/api/auth/register", new RegisterRequest("Test User", email, DefaultPassword));
        register.EnsureSuccessStatusCode();
        var auth = (await register.Content.ReadFromJsonAsync<AuthResponse>(TestJson.Options))!;

        if (role != UserRole.Member)
        {
            var admin = await CreateAdminClientAsync();
            var promote = await admin.PutAsJsonAsync($"/api/users/{auth.UserId}/role", new UpdateUserRoleRequest(role), TestJson.Options);
            promote.EnsureSuccessStatusCode();

            // the role lives inside the token, so log in again to get a token with the new role
            var login = await anonymous.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, DefaultPassword));
            login.EnsureSuccessStatusCode();
            auth = (await login.Content.ReadFromJsonAsync<AuthResponse>(TestJson.Options))!;
        }

        return new TestUser(auth.UserId, email, CreateClient(auth.AccessToken));
    }

    protected static async Task<ProjectDto> CreateProjectAsync(TestUser owner, params int[] memberIds)
    {
        var response = await owner.Client.PostAsJsonAsync("/api/projects", new CreateProjectRequest($"Project {Guid.NewGuid():N}", "test project"));
        response.EnsureSuccessStatusCode();
        var project = (await response.Content.ReadFromJsonAsync<ProjectDto>(TestJson.Options))!;

        foreach (var memberId in memberIds)
        {
            var add = await owner.Client.PostAsJsonAsync($"/api/projects/{project.Id}/members", new AddProjectMemberRequest(memberId));
            add.EnsureSuccessStatusCode();
        }

        return project;
    }

    protected static async Task<TaskDto> CreateTaskAsync(
        TestUser manager,
        int projectId,
        string title = "Test task",
        TaskPriority priority = TaskPriority.Medium,
        int? assigneeId = null)
    {
        var request = new CreateTaskRequest(projectId, title, null, priority, DateTime.UtcNow.AddDays(7), assigneeId);
        var response = await manager.Client.PostAsJsonAsync("/api/tasks", request, TestJson.Options);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<TaskDto>(TestJson.Options))!;
    }
}
