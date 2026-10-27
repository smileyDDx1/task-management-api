using System.Net;
using System.Net.Http.Json;
using TaskManagement.Application.Common.Models;
using TaskManagement.Application.Tasks;
using TaskManagement.Domain.Enums;
using TaskManagement.IntegrationTests.Infrastructure;

namespace TaskManagement.IntegrationTests;

public class TasksEndpointsTests : ApiTestBase
{
    public TasksEndpointsTests(CustomWebApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task CreateTask_AsManagerOfProject_Returns201()
    {
        var manager = await CreateUserAsync(UserRole.Manager);
        var member = await CreateUserAsync();
        var project = await CreateProjectAsync(manager, member.Id);

        var request = new CreateTaskRequest(project.Id, "Write docs", "for the api", TaskPriority.High, DateTime.UtcNow.AddDays(2), member.Id);
        var response = await manager.Client.PostAsJsonAsync("/api/tasks", request, TestJson.Options);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var task = await response.Content.ReadFromJsonAsync<TaskDto>(TestJson.Options);
        Assert.Equal("Write docs", task!.Title);
        Assert.Equal(TaskItemStatus.Todo, task.Status);
        Assert.Equal(member.Id, task.AssigneeId);
        Assert.Equal(manager.Id, task.CreatedById);
    }

    [Fact]
    public async Task CreateTask_AsMember_Returns403()
    {
        var manager = await CreateUserAsync(UserRole.Manager);
        var member = await CreateUserAsync();
        var project = await CreateProjectAsync(manager, member.Id);

        var request = new CreateTaskRequest(project.Id, "Sneaky task", null, TaskPriority.Low, null, null);
        var response = await member.Client.PostAsJsonAsync("/api/tasks", request, TestJson.Options);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CreateTask_DueDateInPast_Returns400()
    {
        var manager = await CreateUserAsync(UserRole.Manager);
        var project = await CreateProjectAsync(manager);

        var request = new CreateTaskRequest(project.Id, "Old task", null, TaskPriority.Low, DateTime.UtcNow.AddDays(-10), null);
        var response = await manager.Client.PostAsJsonAsync("/api/tasks", request, TestJson.Options);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AssignTask_ToUserOutsideProject_Returns400()
    {
        var manager = await CreateUserAsync(UserRole.Manager);
        var outsider = await CreateUserAsync();
        var project = await CreateProjectAsync(manager);
        var task = await CreateTaskAsync(manager, project.Id);

        var response = await manager.Client.PatchAsJsonAsync($"/api/tasks/{task.Id}/assignee", new AssignTaskRequest(outsider.Id));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UpdateStatus_AssigneeCanCompleteOwnTask()
    {
        var manager = await CreateUserAsync(UserRole.Manager);
        var member = await CreateUserAsync();
        var project = await CreateProjectAsync(manager, member.Id);
        var task = await CreateTaskAsync(manager, project.Id, assigneeId: member.Id);

        var response = await member.Client.PatchAsJsonAsync($"/api/tasks/{task.Id}/status",
            new UpdateTaskStatusRequest(TaskItemStatus.Done), TestJson.Options);

        response.EnsureSuccessStatusCode();
        var updated = await response.Content.ReadFromJsonAsync<TaskDto>(TestJson.Options);
        Assert.Equal(TaskItemStatus.Done, updated!.Status);
        Assert.NotNull(updated.CompletedAt);
    }

    [Fact]
    public async Task UpdateStatus_MemberOnSomeoneElsesTask_Returns403()
    {
        var manager = await CreateUserAsync(UserRole.Manager);
        var member = await CreateUserAsync();
        var project = await CreateProjectAsync(manager, member.Id);
        var task = await CreateTaskAsync(manager, project.Id, assigneeId: manager.Id);

        var response = await member.Client.PatchAsJsonAsync($"/api/tasks/{task.Id}/status",
            new UpdateTaskStatusRequest(TaskItemStatus.Done), TestJson.Options);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetTasks_FilterByStatus_ReturnsOnlyMatchingTasks()
    {
        var manager = await CreateUserAsync(UserRole.Manager);
        var project = await CreateProjectAsync(manager);
        for (var i = 0; i < 4; i++)
        {
            await CreateTaskAsync(manager, project.Id, $"Task {i}");
        }
        var done = await CreateTaskAsync(manager, project.Id, "Finished task");
        await manager.Client.PatchAsJsonAsync($"/api/tasks/{done.Id}/status", new UpdateTaskStatusRequest(TaskItemStatus.Done), TestJson.Options);

        var page = await manager.Client.GetFromJsonAsync<PagedResult<TaskDto>>(
            $"/api/tasks?projectId={project.Id}&status=Done", TestJson.Options);

        Assert.Equal(1, page!.TotalCount);
        Assert.Equal("Finished task", page.Items[0].Title);
    }

    [Fact]
    public async Task GetTasks_Paging_ReturnsCorrectPageInfo()
    {
        var manager = await CreateUserAsync(UserRole.Manager);
        var project = await CreateProjectAsync(manager);
        for (var i = 0; i < 5; i++)
        {
            await CreateTaskAsync(manager, project.Id, $"Task {i}");
        }

        var page = await manager.Client.GetFromJsonAsync<PagedResult<TaskDto>>(
            $"/api/tasks?projectId={project.Id}&page=3&pageSize=2", TestJson.Options);

        Assert.Equal(5, page!.TotalCount);
        Assert.Equal(3, page.TotalPages);
        Assert.Single(page.Items);
        Assert.True(page.HasPreviousPage);
        Assert.False(page.HasNextPage);
    }

    [Fact]
    public async Task GetTasks_SortByPriorityDesc_ReturnsHighestFirst()
    {
        var manager = await CreateUserAsync(UserRole.Manager);
        var project = await CreateProjectAsync(manager);
        await CreateTaskAsync(manager, project.Id, "low", TaskPriority.Low);
        await CreateTaskAsync(manager, project.Id, "critical", TaskPriority.Critical);
        await CreateTaskAsync(manager, project.Id, "medium", TaskPriority.Medium);

        var page = await manager.Client.GetFromJsonAsync<PagedResult<TaskDto>>(
            $"/api/tasks?projectId={project.Id}&sortBy=priority&sortDirection=desc", TestJson.Options);

        Assert.Equal(new[] { "critical", "medium", "low" }, page!.Items.Select(t => t.Title).ToArray());
    }

    [Fact]
    public async Task GetTasks_AssignedToMe_ReturnsOnlyMyTasks()
    {
        var manager = await CreateUserAsync(UserRole.Manager);
        var member = await CreateUserAsync();
        var project = await CreateProjectAsync(manager, member.Id);
        await CreateTaskAsync(manager, project.Id, "mine", assigneeId: member.Id);
        await CreateTaskAsync(manager, project.Id, "not mine", assigneeId: manager.Id);

        var page = await member.Client.GetFromJsonAsync<PagedResult<TaskDto>>("/api/tasks?assignedToMe=true", TestJson.Options);

        Assert.Single(page!.Items);
        Assert.Equal("mine", page.Items[0].Title);
    }

    [Fact]
    public async Task GetTasks_MemberOnlySeesTasksFromOwnProjects()
    {
        var manager = await CreateUserAsync(UserRole.Manager);
        var member = await CreateUserAsync();
        var visibleProject = await CreateProjectAsync(manager, member.Id);
        var hiddenProject = await CreateProjectAsync(manager);
        await CreateTaskAsync(manager, visibleProject.Id, "visible");
        await CreateTaskAsync(manager, hiddenProject.Id, "hidden");

        var page = await member.Client.GetFromJsonAsync<PagedResult<TaskDto>>("/api/tasks", TestJson.Options);

        Assert.All(page!.Items, t => Assert.Equal(visibleProject.Id, t.ProjectId));
    }

    [Fact]
    public async Task GetTasks_InvalidSortField_Returns400()
    {
        var member = await CreateUserAsync();

        var response = await member.Client.GetAsync("/api/tasks?sortBy=passwordHash");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task RemoveMember_UnassignsTheirTasks()
    {
        var manager = await CreateUserAsync(UserRole.Manager);
        var member = await CreateUserAsync();
        var project = await CreateProjectAsync(manager, member.Id);
        var task = await CreateTaskAsync(manager, project.Id, assigneeId: member.Id);

        var remove = await manager.Client.DeleteAsync($"/api/projects/{project.Id}/members/{member.Id}");
        var updated = await manager.Client.GetFromJsonAsync<TaskDto>($"/api/tasks/{task.Id}", TestJson.Options);

        Assert.Equal(HttpStatusCode.NoContent, remove.StatusCode);
        Assert.Null(updated!.AssigneeId);
    }

    [Fact]
    public async Task UpdateAndDeleteTask_AsManager_Works()
    {
        var manager = await CreateUserAsync(UserRole.Manager);
        var project = await CreateProjectAsync(manager);
        var task = await CreateTaskAsync(manager, project.Id);

        var update = await manager.Client.PutAsJsonAsync($"/api/tasks/{task.Id}",
            new UpdateTaskRequest("Updated title", "more details", TaskPriority.Critical, null), TestJson.Options);
        var delete = await manager.Client.DeleteAsync($"/api/tasks/{task.Id}");
        var get = await manager.Client.GetAsync($"/api/tasks/{task.Id}");

        update.EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);
    }
}
