using TaskManagement.Application.Common.Models;
using TaskManagement.Domain.Enums;

namespace TaskManagement.Application.Projects;

public record ProjectDto(
    int Id,
    string Name,
    string? Description,
    int OwnerId,
    string OwnerName,
    int MemberCount,
    int TaskCount,
    int OpenTaskCount,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public record ProjectMemberDto(
    int UserId,
    string FullName,
    string Email,
    UserRole Role,
    DateTime JoinedAt);

public record CreateProjectRequest(string Name, string? Description);

public record UpdateProjectRequest(string Name, string? Description);

public record AddProjectMemberRequest(int UserId);

public class ProjectQueryParameters : PaginationParameters
{
    public string? Search { get; set; }
}
