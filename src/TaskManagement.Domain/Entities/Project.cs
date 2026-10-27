using TaskManagement.Domain.Common;

namespace TaskManagement.Domain.Entities;

public class Project : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    public int OwnerId { get; set; }
    public User Owner { get; set; } = null!;

    public ICollection<ProjectMember> Members { get; set; } = new List<ProjectMember>();
    public ICollection<TaskItem> Tasks { get; set; } = new List<TaskItem>();

    public bool IsOwnedBy(int userId) => OwnerId == userId;
}
