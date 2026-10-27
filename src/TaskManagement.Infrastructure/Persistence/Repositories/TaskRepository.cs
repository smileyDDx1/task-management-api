using Microsoft.EntityFrameworkCore;
using TaskManagement.Application.Common.Interfaces;
using TaskManagement.Application.Common.Models;
using TaskManagement.Application.Tasks;
using TaskManagement.Domain.Entities;

namespace TaskManagement.Infrastructure.Persistence.Repositories;

public class TaskRepository : ITaskRepository
{
    private readonly AppDbContext _context;

    public TaskRepository(AppDbContext context)
    {
        _context = context;
    }

    public Task<TaskItem?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return _context.Tasks.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
    }

    public async Task<TaskDto?> GetDtoByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var task = await _context.Tasks
            .Include(t => t.Project)
            .Include(t => t.Assignee)
            .Include(t => t.CreatedBy)
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

        return task is null ? null : MapToDto(task, DateTime.UtcNow);
    }

    public async Task<PagedResult<TaskDto>> GetPagedAsync(TaskQueryParameters query, int? visibleToUserId, CancellationToken cancellationToken = default)
    {
        var tasks = _context.Tasks
            .Include(t => t.Project)
            .Include(t => t.Assignee)
            .Include(t => t.CreatedBy)
            .AsQueryable();

        if (visibleToUserId.HasValue)
            tasks = tasks.Where(t => t.Project.Members.Any(m => m.UserId == visibleToUserId.Value));

        if (query.ProjectId.HasValue)
            tasks = tasks.Where(t => t.ProjectId == query.ProjectId.Value);

        if (query.Status.HasValue)
            tasks = tasks.Where(t => t.Status == query.Status.Value);

        if (query.Priority.HasValue)
            tasks = tasks.Where(t => t.Priority == query.Priority.Value);

        if (query.AssigneeId.HasValue)
            tasks = tasks.Where(t => t.AssigneeId == query.AssigneeId.Value);

        if (query.DueFrom.HasValue)
            tasks = tasks.Where(t => t.DueDate >= query.DueFrom.Value);

        if (query.DueTo.HasValue)
            tasks = tasks.Where(t => t.DueDate <= query.DueTo.Value);

        if (!string.IsNullOrWhiteSpace(query.Search))
            tasks = tasks.Where(t => t.Title.Contains(query.Search.Trim()));

        var list = await tasks.ToListAsync(cancellationToken);
        var now = DateTime.UtcNow;

        if (query.IsOverdue.HasValue)
            list = list.Where(t => t.IsOverdue(now) == query.IsOverdue.Value).ToList();

        var items = Sort(list, query)
            .Skip(query.Skip)
            .Take(query.PageSize)
            .Select(t => MapToDto(t, now))
            .ToList();

        return new PagedResult<TaskDto>(items, list.Count, query.Page, query.PageSize);
    }

    public Task<int> UnassignUserInProjectAsync(int projectId, int userId, CancellationToken cancellationToken = default)
    {
        // ExecuteUpdate runs a single UPDATE statement instead of loading every task
        return _context.Tasks
            .Where(t => t.ProjectId == projectId && t.AssigneeId == userId)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.AssigneeId, (int?)null), cancellationToken);
    }

    public async Task AddAsync(TaskItem task, CancellationToken cancellationToken = default)
    {
        await _context.Tasks.AddAsync(task, cancellationToken);
    }

    public void Remove(TaskItem task)
    {
        _context.Tasks.Remove(task);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return _context.SaveChangesAsync(cancellationToken);
    }

    private static IEnumerable<TaskItem> Sort(IEnumerable<TaskItem> tasks, TaskQueryParameters query)
    {
        var desc = query.SortDirection.Equals("desc", StringComparison.OrdinalIgnoreCase);

        return query.SortBy.ToLowerInvariant() switch
        {
            "duedate" => desc ? tasks.OrderByDescending(t => t.DueDate) : tasks.OrderBy(t => t.DueDate),
            "priority" => desc ? tasks.OrderByDescending(t => t.Priority) : tasks.OrderBy(t => t.Priority),
            "title" => desc ? tasks.OrderByDescending(t => t.Title) : tasks.OrderBy(t => t.Title),
            "status" => desc ? tasks.OrderByDescending(t => t.Status) : tasks.OrderBy(t => t.Status),
            _ => desc ? tasks.OrderByDescending(t => t.CreatedAt) : tasks.OrderBy(t => t.CreatedAt)
        };
    }

    private static TaskDto MapToDto(TaskItem t, DateTime now)
    {
        return new TaskDto(
            t.Id,
            t.Title,
            t.Description,
            t.Status,
            t.Priority,
            t.DueDate,
            t.CompletedAt,
            t.IsOverdue(now),
            t.ProjectId,
            t.Project.Name,
            t.AssigneeId,
            t.Assignee?.FullName,
            t.CreatedById,
            t.CreatedBy.FullName,
            t.CreatedAt,
            t.UpdatedAt);
    }
}
