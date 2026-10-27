using System.Net;
using System.Net.Http.Json;
using TaskManagement.Application.Common.Models;
using TaskManagement.Application.Projects;
using TaskManagement.Domain.Enums;
using TaskManagement.IntegrationTests.Infrastructure;

namespace TaskManagement.IntegrationTests;

public class ProjectsEndpointsTests : ApiTestBase
{
    public ProjectsEndpointsTests(CustomWebApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task CreateProject_AsMember_Returns403()
    {
        var member = await CreateUserAsync();

        var response = await member.Client.PostAsJsonAsync("/api/projects", new CreateProjectRequest("My project", null));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CreateProject_AsManager_Returns201AndOwnerIsMember()
    {
        var manager = await CreateUserAsync(UserRole.Manager);

        var response = await manager.Client.PostAsJsonAsync("/api/projects", new CreateProjectRequest("Website redesign", "Q4 work"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        var project = await response.Content.ReadFromJsonAsync<ProjectDto>(TestJson.Options);
        Assert.Equal(manager.Id, project!.OwnerId);
        Assert.Equal(1, project.MemberCount);
    }

    [Fact]
    public async Task CreateProject_EmptyName_Returns400()
    {
        var manager = await CreateUserAsync(UserRole.Manager);

        var response = await manager.Client.PostAsJsonAsync("/api/projects", new CreateProjectRequest("", null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetProject_NotAMember_Returns403()
    {
        var manager = await CreateUserAsync(UserRole.Manager);
        var outsider = await CreateUserAsync();
        var project = await CreateProjectAsync(manager);

        var response = await outsider.Client.GetAsync($"/api/projects/{project.Id}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetProject_DoesNotExist_Returns404()
    {
        var admin = await CreateAdminClientAsync();

        var response = await admin.GetAsync("/api/projects/999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task AddMember_MemberCanThenSeeProjectInList()
    {
        var manager = await CreateUserAsync(UserRole.Manager);
        var member = await CreateUserAsync();
        var project = await CreateProjectAsync(manager, member.Id);

        var page = await member.Client.GetFromJsonAsync<PagedResult<ProjectDto>>("/api/projects", TestJson.Options);
        var members = await member.Client.GetFromJsonAsync<List<ProjectMemberDto>>($"/api/projects/{project.Id}/members", TestJson.Options);

        Assert.Contains(page!.Items, p => p.Id == project.Id);
        Assert.Equal(2, members!.Count);
    }

    [Fact]
    public async Task AddMember_Twice_Returns409()
    {
        var manager = await CreateUserAsync(UserRole.Manager);
        var member = await CreateUserAsync();
        var project = await CreateProjectAsync(manager, member.Id);

        var response = await manager.Client.PostAsJsonAsync($"/api/projects/{project.Id}/members", new AddProjectMemberRequest(member.Id));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task UpdateProject_OtherManager_Returns403()
    {
        var owner = await CreateUserAsync(UserRole.Manager);
        var otherManager = await CreateUserAsync(UserRole.Manager);
        var project = await CreateProjectAsync(owner);

        var response = await otherManager.Client.PutAsJsonAsync($"/api/projects/{project.Id}", new UpdateProjectRequest("Hijacked", null));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UpdateProject_Owner_ReturnsUpdatedProject()
    {
        var owner = await CreateUserAsync(UserRole.Manager);
        var project = await CreateProjectAsync(owner);

        var response = await owner.Client.PutAsJsonAsync($"/api/projects/{project.Id}", new UpdateProjectRequest("Renamed", "new description"));

        response.EnsureSuccessStatusCode();
        var updated = await response.Content.ReadFromJsonAsync<ProjectDto>(TestJson.Options);
        Assert.Equal("Renamed", updated!.Name);
        Assert.NotNull(updated.UpdatedAt);
    }

    [Fact]
    public async Task DeleteProject_AlsoDeletesItsTasks()
    {
        var owner = await CreateUserAsync(UserRole.Manager);
        var project = await CreateProjectAsync(owner);
        var task = await CreateTaskAsync(owner, project.Id);

        var delete = await owner.Client.DeleteAsync($"/api/projects/{project.Id}");
        var getTask = await owner.Client.GetAsync($"/api/tasks/{task.Id}");

        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, getTask.StatusCode);
    }

    [Fact]
    public async Task GetProjects_SearchByName_FiltersResults()
    {
        var manager = await CreateUserAsync(UserRole.Manager);
        await CreateProjectAsync(manager);
        var response = await manager.Client.PostAsJsonAsync("/api/projects", new CreateProjectRequest("Mobile App Launch", null));
        response.EnsureSuccessStatusCode();

        var page = await manager.Client.GetFromJsonAsync<PagedResult<ProjectDto>>("/api/projects?search=Mobile", TestJson.Options);

        Assert.Single(page!.Items);
        Assert.Equal("Mobile App Launch", page.Items[0].Name);
    }
}
