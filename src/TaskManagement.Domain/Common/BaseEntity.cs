namespace TaskManagement.Domain.Common;

public abstract class BaseEntity
{
    public int Id { get; set; }

    // set automatically in AppDbContext.SaveChangesAsync
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
