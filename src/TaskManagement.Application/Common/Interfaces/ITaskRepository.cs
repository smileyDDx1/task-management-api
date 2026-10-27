using TaskManagement.Application.Common.Models;
using TaskManagement.Application.Tasks;
using TaskManagement.Domain.Entities;

namespace TaskManagement.Application.Common.Interfaces;

public interface ITaskRepository
{
    Task<TaskItem?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<TaskDto?> GetDtoByIdAsync(int id, CancellationToken cancellationToken = default);

    // visibleToUserId = null means admin (all tasks), otherwise only tasks from projects the user is a member of
    Task<PagedResult<TaskDto>> GetPagedAsync(TaskQueryParameters query, int? visibleToUserId, CancellationToken cancellationToken = default);

    // when someone leaves a project their open tasks go back to unassigned
    Task<int> UnassignUserInProjectAsync(int projectId, int userId, CancellationToken cancellationToken = default);

    Task AddAsync(TaskItem task, CancellationToken cancellationToken = default);
    void Remove(TaskItem task);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
