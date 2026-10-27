using TaskManagement.Application.Common.Models;

namespace TaskManagement.Application.Tasks;

public interface ITaskService
{
    Task<PagedResult<TaskDto>> GetTasksAsync(TaskQueryParameters query, CancellationToken cancellationToken = default);
    Task<TaskDto> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<TaskDto> CreateAsync(CreateTaskRequest request, CancellationToken cancellationToken = default);
    Task<TaskDto> UpdateAsync(int id, UpdateTaskRequest request, CancellationToken cancellationToken = default);
    Task<TaskDto> UpdateStatusAsync(int id, UpdateTaskStatusRequest request, CancellationToken cancellationToken = default);
    Task<TaskDto> AssignAsync(int id, AssignTaskRequest request, CancellationToken cancellationToken = default);
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
}
