using TaskManagement.Application.Common.Models;
using TaskManagement.Application.Projects;
using TaskManagement.Domain.Entities;

namespace TaskManagement.Application.Common.Interfaces;

public interface IProjectRepository
{
    // tracked entity, use this when you want to change something and save
    Task<Project?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    // read only version mapped to a dto
    Task<ProjectDto?> GetDtoByIdAsync(int id, CancellationToken cancellationToken = default);

    // memberUserId = null means no filter (admin can see everything)
    Task<PagedResult<ProjectDto>> GetPagedAsync(ProjectQueryParameters query, int? memberUserId, CancellationToken cancellationToken = default);

    Task<bool> IsMemberAsync(int projectId, int userId, CancellationToken cancellationToken = default);
    Task<ProjectMember?> GetMemberAsync(int projectId, int userId, CancellationToken cancellationToken = default);
    Task<List<ProjectMemberDto>> GetMembersAsync(int projectId, CancellationToken cancellationToken = default);

    Task AddAsync(Project project, CancellationToken cancellationToken = default);
    void Remove(Project project);
    Task AddMemberAsync(ProjectMember member, CancellationToken cancellationToken = default);
    void RemoveMember(ProjectMember member);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
