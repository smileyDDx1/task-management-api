using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TaskManagement.Application.Common.Exceptions;
using TaskManagement.Application.Common.Interfaces;
using TaskManagement.Application.Tasks;
using TaskManagement.Domain.Entities;
using TaskManagement.Domain.Enums;
using TaskManagement.UnitTests.Helpers;

namespace TaskManagement.UnitTests.Tasks;

public class TaskServiceTests
{
    private const int ProjectId = 1;
    private const int ManagerId = 10;
    private const int MemberId = 20;

    private readonly ITaskRepository _taskRepository = Substitute.For<ITaskRepository>();
    private readonly IProjectRepository _projectRepository = Substitute.For<IProjectRepository>();

    public TaskServiceTests()
    {
        _projectRepository.GetByIdAsync(ProjectId, Arg.Any<CancellationToken>())
            .Returns(new Project { Id = ProjectId, OwnerId = ManagerId });
        _projectRepository.IsMemberAsync(ProjectId, ManagerId, Arg.Any<CancellationToken>()).Returns(true);
        _projectRepository.IsMemberAsync(ProjectId, MemberId, Arg.Any<CancellationToken>()).Returns(true);

        _taskRepository.GetDtoByIdAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(SampleDto());
    }

    private TaskService CreateService(int userId, UserRole role) =>
        new(_taskRepository, _projectRepository, TestUsers.CurrentUser(userId, role), NullLogger<TaskService>.Instance);

    private static TaskDto SampleDto() => new(
        5, "Fix login", null, TaskItemStatus.Todo, TaskPriority.High, null, null, false,
        ProjectId, "Website", null, null, ManagerId, "Manager", DateTime.UtcNow, null);

    private TaskItem ExistingTask(int? assigneeId = null)
    {
        var task = new TaskItem { Id = 5, ProjectId = ProjectId, Title = "Fix login", AssigneeId = assigneeId, CreatedById = ManagerId };
        _taskRepository.GetByIdAsync(5, Arg.Any<CancellationToken>()).Returns(task);
        return task;
    }

    [Fact]
    public async Task GetTasksAsync_AssignedToMe_FiltersByCurrentUser()
    {
        var query = new TaskQueryParameters { AssignedToMe = true };
        var sut = CreateService(MemberId, UserRole.Member);

        await sut.GetTasksAsync(query);

        Assert.Equal(MemberId, query.AssigneeId);
        await _taskRepository.Received(1).GetPagedAsync(query, MemberId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetTasksAsync_AsAdmin_SeesAllTasks()
    {
        var query = new TaskQueryParameters();
        var sut = CreateService(1, UserRole.Admin);

        await sut.GetTasksAsync(query);

        await _taskRepository.Received(1).GetPagedAsync(query, null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetByIdAsync_NotFound_Throws()
    {
        _taskRepository.GetDtoByIdAsync(99, Arg.Any<CancellationToken>()).Returns((TaskDto?)null);
        var sut = CreateService(1, UserRole.Admin);

        await Assert.ThrowsAsync<NotFoundException>(() => sut.GetByIdAsync(99));
    }

    [Fact]
    public async Task GetByIdAsync_UserNotInProject_ThrowsForbidden()
    {
        var sut = CreateService(999, UserRole.Member);

        await Assert.ThrowsAsync<ForbiddenException>(() => sut.GetByIdAsync(5));
    }

    [Fact]
    public async Task CreateAsync_ValidRequest_SavesTaskWithCreator()
    {
        var sut = CreateService(ManagerId, UserRole.Manager);
        var request = new CreateTaskRequest(ProjectId, " Fix login ", null, TaskPriority.High, DateTime.UtcNow.AddDays(3), MemberId);

        var result = await sut.CreateAsync(request);

        await _taskRepository.Received(1).AddAsync(
            Arg.Is<TaskItem>(t =>
                t.Title == "Fix login" &&
                t.CreatedById == ManagerId &&
                t.AssigneeId == MemberId &&
                t.Status == TaskItemStatus.Todo),
            Arg.Any<CancellationToken>());
        Assert.Equal(5, result.Id);
    }

    [Fact]
    public async Task CreateAsync_ProjectDoesNotExist_ThrowsNotFound()
    {
        var sut = CreateService(ManagerId, UserRole.Manager);
        var request = new CreateTaskRequest(404, "Task", null, TaskPriority.Low, null, null);

        await Assert.ThrowsAsync<NotFoundException>(() => sut.CreateAsync(request));
    }

    [Fact]
    public async Task CreateAsync_ManagerNotInProject_ThrowsForbidden()
    {
        var sut = CreateService(77, UserRole.Manager);
        var request = new CreateTaskRequest(ProjectId, "Task", null, TaskPriority.Low, null, null);

        await Assert.ThrowsAsync<ForbiddenException>(() => sut.CreateAsync(request));
    }

    [Fact]
    public async Task CreateAsync_MemberRole_ThrowsForbidden()
    {
        // members are in the project but can't create tasks
        var sut = CreateService(MemberId, UserRole.Member);
        var request = new CreateTaskRequest(ProjectId, "Task", null, TaskPriority.Low, null, null);

        await Assert.ThrowsAsync<ForbiddenException>(() => sut.CreateAsync(request));
    }

    [Fact]
    public async Task CreateAsync_AssigneeNotInProject_ThrowsBadRequest()
    {
        var sut = CreateService(ManagerId, UserRole.Manager);
        var request = new CreateTaskRequest(ProjectId, "Task", null, TaskPriority.Low, null, AssigneeId: 555);

        await Assert.ThrowsAsync<BadRequestException>(() => sut.CreateAsync(request));
        await _taskRepository.DidNotReceive().AddAsync(Arg.Any<TaskItem>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateAsync_Manager_UpdatesFields()
    {
        var task = ExistingTask();
        var sut = CreateService(ManagerId, UserRole.Manager);
        var due = new DateTime(2030, 5, 1);

        await sut.UpdateAsync(5, new UpdateTaskRequest("New title", "details", TaskPriority.Critical, due));

        Assert.Equal("New title", task.Title);
        Assert.Equal(TaskPriority.Critical, task.Priority);
        Assert.Equal(due, task.DueDate);
        await _taskRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateStatusAsync_AssigneeCanMoveOwnTask()
    {
        var task = ExistingTask(assigneeId: MemberId);
        var sut = CreateService(MemberId, UserRole.Member);

        await sut.UpdateStatusAsync(5, new UpdateTaskStatusRequest(TaskItemStatus.InProgress));

        Assert.Equal(TaskItemStatus.InProgress, task.Status);
    }

    [Fact]
    public async Task UpdateStatusAsync_MemberNotAssignee_ThrowsForbidden()
    {
        ExistingTask(assigneeId: 30);
        var sut = CreateService(MemberId, UserRole.Member);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            sut.UpdateStatusAsync(5, new UpdateTaskStatusRequest(TaskItemStatus.Done)));
    }

    [Fact]
    public async Task UpdateStatusAsync_ToDone_SetsCompletedAt()
    {
        var task = ExistingTask();
        var sut = CreateService(ManagerId, UserRole.Manager);

        await sut.UpdateStatusAsync(5, new UpdateTaskStatusRequest(TaskItemStatus.Done));

        Assert.NotNull(task.CompletedAt);
    }

    [Fact]
    public async Task AssignAsync_NullAssignee_UnassignsTask()
    {
        var task = ExistingTask(assigneeId: MemberId);
        var sut = CreateService(ManagerId, UserRole.Manager);

        await sut.AssignAsync(5, new AssignTaskRequest(null));

        Assert.Null(task.AssigneeId);
    }

    [Fact]
    public async Task AssignAsync_UserOutsideProject_ThrowsBadRequest()
    {
        ExistingTask();
        var sut = CreateService(1, UserRole.Admin);

        await Assert.ThrowsAsync<BadRequestException>(() => sut.AssignAsync(5, new AssignTaskRequest(555)));
    }

    [Fact]
    public async Task DeleteAsync_Admin_RemovesTask()
    {
        var task = ExistingTask();
        var sut = CreateService(1, UserRole.Admin);

        await sut.DeleteAsync(5);

        _taskRepository.Received(1).Remove(task);
    }

    [Fact]
    public async Task DeleteAsync_TaskDoesNotExist_ThrowsNotFound()
    {
        _taskRepository.GetByIdAsync(5, Arg.Any<CancellationToken>()).Returns((TaskItem?)null);
        var sut = CreateService(1, UserRole.Admin);

        await Assert.ThrowsAsync<NotFoundException>(() => sut.DeleteAsync(5));
    }
}
