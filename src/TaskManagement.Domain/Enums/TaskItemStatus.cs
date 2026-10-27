namespace TaskManagement.Domain.Enums;

// named TaskItemStatus instead of TaskStatus because System.Threading.Tasks.TaskStatus already exists
public enum TaskItemStatus
{
    Todo = 0,
    InProgress = 1,
    InReview = 2,
    Done = 3
}
