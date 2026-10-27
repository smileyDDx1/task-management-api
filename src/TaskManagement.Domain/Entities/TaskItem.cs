using TaskManagement.Domain.Common;
using TaskManagement.Domain.Enums;

namespace TaskManagement.Domain.Entities;

// called TaskItem so it doesn't clash with System.Threading.Tasks.Task
public class TaskItem : BaseEntity
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public TaskItemStatus Status { get; set; } = TaskItemStatus.Todo;
    public TaskPriority Priority { get; set; } = TaskPriority.Medium;
    public DateTime? DueDate { get; set; }
    public DateTime? CompletedAt { get; private set; }

    public int ProjectId { get; set; }
    public Project Project { get; set; } = null!;

    public int? AssigneeId { get; set; }
    public User? Assignee { get; set; }

    public int CreatedById { get; set; }
    public User CreatedBy { get; set; } = null!;

    public void ChangeStatus(TaskItemStatus newStatus)
    {
        if (Status == newStatus)
            return;

        Status = newStatus;

        // keep track of when the task was finished, clear it if someone reopens the task
        CompletedAt = newStatus == TaskItemStatus.Done ? DateTime.UtcNow : null;
    }

    public bool IsOverdue(DateTime utcNow)
    {
        return DueDate.HasValue && DueDate.Value < utcNow && Status != TaskItemStatus.Done;
    }
}
