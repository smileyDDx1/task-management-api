namespace TaskManagement.Application.Common.Models;

public abstract class PaginationParameters
{
    public const int MaxPageSize = 50;

    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;

    // used by Skip() in the repositories
    public int Skip => (Page - 1) * PageSize;
}
