using TaskManagement.Domain.Entities;
using TaskManagement.Domain.Enums;

namespace TaskManagement.UnitTests.Domain;

public class TaskItemTests
{
    private static readonly DateTime Now = new(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void ChangeStatus_ToDone_SetsCompletedAt()
    {
        var task = new TaskItem();

        task.ChangeStatus(TaskItemStatus.Done);

        Assert.Equal(TaskItemStatus.Done, task.Status);
        Assert.NotNull(task.CompletedAt);
    }

    [Fact]
    public void ChangeStatus_ReopeningDoneTask_ClearsCompletedAt()
    {
        var task = new TaskItem();
        task.ChangeStatus(TaskItemStatus.Done);

        task.ChangeStatus(TaskItemStatus.InProgress);

        Assert.Null(task.CompletedAt);
    }

    [Fact]
    public void ChangeStatus_SameStatus_DoesNotChangeCompletedAt()
    {
        var task = new TaskItem();
        task.ChangeStatus(TaskItemStatus.Done);
        var completedAt = task.CompletedAt;

        task.ChangeStatus(TaskItemStatus.Done);

        Assert.Equal(completedAt, task.CompletedAt);
    }

    [Theory]
    [InlineData(-1, TaskItemStatus.Todo, true)]        // due yesterday, still open
    [InlineData(-1, TaskItemStatus.InReview, true)]
    [InlineData(-1, TaskItemStatus.Done, false)]       // finished tasks are never overdue
    [InlineData(1, TaskItemStatus.Todo, false)]        // due tomorrow
    public void IsOverdue_DependsOnDueDateAndStatus(int dueInDays, TaskItemStatus status, bool expected)
    {
        var task = new TaskItem { DueDate = Now.AddDays(dueInDays) };
        task.ChangeStatus(status);

        Assert.Equal(expected, task.IsOverdue(Now));
    }

    [Fact]
    public void IsOverdue_NoDueDate_ReturnsFalse()
    {
        Assert.False(new TaskItem { DueDate = null }.IsOverdue(Now));
    }

    [Fact]
    public void Project_IsOwnedBy_ChecksOwnerId()
    {
        var project = new Project { OwnerId = 3 };

        Assert.True(project.IsOwnedBy(3));
        Assert.False(project.IsOwnedBy(4));
    }
}
