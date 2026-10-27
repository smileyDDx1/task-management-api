using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using TaskManagement.Application.Common.Interfaces;
using TaskManagement.Application.Common.Models;
using TaskManagement.Application.Tasks;
using TaskManagement.Domain.Entities;
using TaskManagement.Domain.Enums;

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

    public Task<TaskDto?> GetDtoByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return _context.Tasks
            .AsNoTracking()
            .Where(t => t.Id == id)
            .Select(ToDto(DateTime.UtcNow))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<PagedResult<TaskDto>> GetPagedAsync(TaskQueryParameters query, int? visibleToUserId, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        // AsNoTracking: we only read here, so EF doesn't need to keep snapshots of every entity
        var tasks = _context.Tasks.AsNoTracking();

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

        if (query.IsOverdue == true)
            tasks = tasks.Where(t => t.DueDate != null && t.DueDate < now && t.Status != TaskItemStatus.Done);
        else if (query.IsOverdue == false)
            tasks = tasks.Where(t => t.DueDate == null || t.DueDate >= now || t.Status == TaskItemStatus.Done);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            tasks = tasks.Where(t => t.Title.Contains(search));
        }

        // COUNT(*) runs in the database instead of loading every row to count it
        var totalCount = await tasks.CountAsync(cancellationToken);

        // sorting + OFFSET/FETCH in SQL, and Select() only reads the columns the dto needs
        // (no Include needed, EF turns t.Project.Name into a join)
        var items = await ApplySorting(tasks, query)
            .Skip(query.Skip)
            .Take(query.PageSize)
            .Select(ToDto(now))
            .ToListAsync(cancellationToken);

        return new PagedResult<TaskDto>(items, totalCount, query.Page, query.PageSize);
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

    private static IQueryable<TaskItem> ApplySorting(IQueryable<TaskItem> tasks, TaskQueryParameters query)
    {
        var desc = query.SortDirection.Equals("desc", StringComparison.OrdinalIgnoreCase);

        IOrderedQueryable<TaskItem> ordered = query.SortBy.ToLowerInvariant() switch
        {
            "duedate" => desc ? tasks.OrderByDescending(t => t.DueDate) : tasks.OrderBy(t => t.DueDate),
            "priority" => desc ? tasks.OrderByDescending(t => t.Priority) : tasks.OrderBy(t => t.Priority),
            "title" => desc ? tasks.OrderByDescending(t => t.Title) : tasks.OrderBy(t => t.Title),
            "status" => desc ? tasks.OrderByDescending(t => t.Status) : tasks.OrderBy(t => t.Status),
            _ => desc ? tasks.OrderByDescending(t => t.CreatedAt) : tasks.OrderBy(t => t.CreatedAt)
        };

        // tie breaker so paging is stable when many rows have the same value
        return ordered.ThenBy(t => t.Id);
    }

    private static Expression<Func<TaskItem, TaskDto>> ToDto(DateTime now) => t => new TaskDto(
        t.Id,
        t.Title,
        t.Description,
        t.Status,
        t.Priority,
        t.DueDate,
        t.CompletedAt,
        t.DueDate != null && t.DueDate < now && t.Status != TaskItemStatus.Done,
        t.ProjectId,
        t.Project.Name,
        t.AssigneeId,
        t.Assignee != null ? t.Assignee.FullName : null,
        t.CreatedById,
        t.CreatedBy.FullName,
        t.CreatedAt,
        t.UpdatedAt);
}
