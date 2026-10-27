using TaskManagement.Application.Common.Models;
using TaskManagement.Domain.Enums;

namespace TaskManagement.Application.Tasks;

public record TaskDto(
    int Id,
    string Title,
    string? Description,
    TaskItemStatus Status,
    TaskPriority Priority,
    DateTime? DueDate,
    DateTime? CompletedAt,
    bool IsOverdue,
    int ProjectId,
    string ProjectName,
    int? AssigneeId,
    string? AssigneeName,
    int CreatedById,
    string CreatedByName,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public record CreateTaskRequest(
    int ProjectId,
    string Title,
    string? Description,
    TaskPriority Priority,
    DateTime? DueDate,
    int? AssigneeId);

public record UpdateTaskRequest(
    string Title,
    string? Description,
    TaskPriority Priority,
    DateTime? DueDate);

public record UpdateTaskStatusRequest(TaskItemStatus Status);

// AssigneeId = null removes the current assignee
public record AssignTaskRequest(int? AssigneeId);

public class TaskQueryParameters : PaginationParameters
{
    public static readonly string[] AllowedSortFields = ["createdAt", "dueDate", "priority", "title", "status"];

    public int? ProjectId { get; set; }
    public TaskItemStatus? Status { get; set; }
    public TaskPriority? Priority { get; set; }
    public int? AssigneeId { get; set; }

    // shortcut for "tasks assigned to me", overrides AssigneeId
    public bool AssignedToMe { get; set; }

    public DateTime? DueFrom { get; set; }
    public DateTime? DueTo { get; set; }
    public bool? IsOverdue { get; set; }

    // searches in the title
    public string? Search { get; set; }

    public string SortBy { get; set; } = "createdAt";
    public string SortDirection { get; set; } = "desc";
}
