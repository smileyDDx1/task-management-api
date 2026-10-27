using Microsoft.Extensions.Logging;
using TaskManagement.Application.Common.Exceptions;
using TaskManagement.Application.Common.Interfaces;
using TaskManagement.Application.Common.Models;
using TaskManagement.Domain.Entities;
using TaskManagement.Domain.Enums;

namespace TaskManagement.Application.Tasks;

public class TaskService : ITaskService
{
    private readonly ITaskRepository _taskRepository;
    private readonly IProjectRepository _projectRepository;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<TaskService> _logger;

    public TaskService(
        ITaskRepository taskRepository,
        IProjectRepository projectRepository,
        ICurrentUserService currentUser,
        ILogger<TaskService> logger)
    {
        _taskRepository = taskRepository;
        _projectRepository = projectRepository;
        _currentUser = currentUser;
        _logger = logger;
    }

    private bool IsAdmin => _currentUser.Role == UserRole.Admin;

    public Task<PagedResult<TaskDto>> GetTasksAsync(TaskQueryParameters query, CancellationToken cancellationToken = default)
    {
        if (query.AssignedToMe)
        {
            query.AssigneeId = _currentUser.UserId;
        }

        int? visibleTo = IsAdmin ? null : _currentUser.UserId;
        return _taskRepository.GetPagedAsync(query, visibleTo, cancellationToken);
    }

    public async Task<TaskDto> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var task = await _taskRepository.GetDtoByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException("Task", id);

        if (!IsAdmin && !await _projectRepository.IsMemberAsync(task.ProjectId, _currentUser.UserId, cancellationToken))
        {
            throw new ForbiddenException("You are not a member of this task's project.");
        }

        return task;
    }

    public async Task<TaskDto> CreateAsync(CreateTaskRequest request, CancellationToken cancellationToken = default)
    {
        _ = await _projectRepository.GetByIdAsync(request.ProjectId, cancellationToken)
            ?? throw new NotFoundException("Project", request.ProjectId);

        await EnsureCanManageTasksAsync(request.ProjectId, cancellationToken);

        if (request.AssigneeId.HasValue)
        {
            await EnsureAssigneeIsMemberAsync(request.ProjectId, request.AssigneeId.Value, cancellationToken);
        }

        var task = new TaskItem
        {
            ProjectId = request.ProjectId,
            Title = request.Title.Trim(),
            Description = request.Description?.Trim(),
            Priority = request.Priority,
            DueDate = request.DueDate,
            AssigneeId = request.AssigneeId,
            CreatedById = _currentUser.UserId
        };

        await _taskRepository.AddAsync(task, cancellationToken);
        await _taskRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Task {TaskId} created in project {ProjectId} by user {UserId}",
            task.Id, task.ProjectId, _currentUser.UserId);

        return (await _taskRepository.GetDtoByIdAsync(task.Id, cancellationToken))!;
    }

    public async Task<TaskDto> UpdateAsync(int id, UpdateTaskRequest request, CancellationToken cancellationToken = default)
    {
        var task = await GetTaskOrThrowAsync(id, cancellationToken);
        await EnsureCanManageTasksAsync(task.ProjectId, cancellationToken);

        task.Title = request.Title.Trim();
        task.Description = request.Description?.Trim();
        task.Priority = request.Priority;
        task.DueDate = request.DueDate;

        await _taskRepository.SaveChangesAsync(cancellationToken);

        return (await _taskRepository.GetDtoByIdAsync(id, cancellationToken))!;
    }

    public async Task<TaskDto> UpdateStatusAsync(int id, UpdateTaskStatusRequest request, CancellationToken cancellationToken = default)
    {
        var task = await GetTaskOrThrowAsync(id, cancellationToken);

        // members can move their own tasks along the board,
        // managers/admins can change the status of any task in the project
        var isAssignee = task.AssigneeId == _currentUser.UserId;
        if (!isAssignee)
        {
            await EnsureCanManageTasksAsync(task.ProjectId, cancellationToken);
        }

        var oldStatus = task.Status;
        task.ChangeStatus(request.Status);
        await _taskRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Task {TaskId} status changed from {OldStatus} to {NewStatus} by user {UserId}",
            task.Id, oldStatus, task.Status, _currentUser.UserId);

        return (await _taskRepository.GetDtoByIdAsync(id, cancellationToken))!;
    }

    public async Task<TaskDto> AssignAsync(int id, AssignTaskRequest request, CancellationToken cancellationToken = default)
    {
        var task = await GetTaskOrThrowAsync(id, cancellationToken);
        await EnsureCanManageTasksAsync(task.ProjectId, cancellationToken);

        if (request.AssigneeId.HasValue)
        {
            await EnsureAssigneeIsMemberAsync(task.ProjectId, request.AssigneeId.Value, cancellationToken);
        }

        task.AssigneeId = request.AssigneeId;
        await _taskRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Task {TaskId} assigned to {AssigneeId}", task.Id, task.AssigneeId);

        return (await _taskRepository.GetDtoByIdAsync(id, cancellationToken))!;
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var task = await GetTaskOrThrowAsync(id, cancellationToken);
        await EnsureCanManageTasksAsync(task.ProjectId, cancellationToken);

        _taskRepository.Remove(task);
        await _taskRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Task {TaskId} deleted by user {UserId}", id, _currentUser.UserId);
    }

    private async Task<TaskItem> GetTaskOrThrowAsync(int id, CancellationToken cancellationToken)
    {
        return await _taskRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException("Task", id);
    }

    // admin can manage any task, a manager only tasks in projects they belong to
    private async Task EnsureCanManageTasksAsync(int projectId, CancellationToken cancellationToken)
    {
        if (IsAdmin)
            return;

        var isMember = await _projectRepository.IsMemberAsync(projectId, _currentUser.UserId, cancellationToken);
        if (_currentUser.Role != UserRole.Manager || !isMember)
        {
            throw new ForbiddenException("Only an admin or a manager of this project can do this.");
        }
    }

    private async Task EnsureAssigneeIsMemberAsync(int projectId, int assigneeId, CancellationToken cancellationToken)
    {
        if (!await _projectRepository.IsMemberAsync(projectId, assigneeId, cancellationToken))
        {
            throw new BadRequestException("Tasks can only be assigned to members of the project.");
        }
    }
}
