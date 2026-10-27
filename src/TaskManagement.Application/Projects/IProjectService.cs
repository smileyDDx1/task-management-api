using TaskManagement.Application.Common.Models;

namespace TaskManagement.Application.Projects;

public interface IProjectService
{
    Task<PagedResult<ProjectDto>> GetProjectsAsync(ProjectQueryParameters query, CancellationToken cancellationToken = default);
    Task<ProjectDto> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<ProjectDto> CreateAsync(CreateProjectRequest request, CancellationToken cancellationToken = default);
    Task<ProjectDto> UpdateAsync(int id, UpdateProjectRequest request, CancellationToken cancellationToken = default);
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);

    Task<List<ProjectMemberDto>> GetMembersAsync(int projectId, CancellationToken cancellationToken = default);
    Task AddMemberAsync(int projectId, AddProjectMemberRequest request, CancellationToken cancellationToken = default);
    Task RemoveMemberAsync(int projectId, int userId, CancellationToken cancellationToken = default);
}
