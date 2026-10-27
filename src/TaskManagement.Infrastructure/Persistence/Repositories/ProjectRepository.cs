using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using TaskManagement.Application.Common.Interfaces;
using TaskManagement.Application.Common.Models;
using TaskManagement.Application.Projects;
using TaskManagement.Domain.Entities;
using TaskManagement.Domain.Enums;

namespace TaskManagement.Infrastructure.Persistence.Repositories;

public class ProjectRepository : IProjectRepository
{
    private readonly AppDbContext _context;

    public ProjectRepository(AppDbContext context)
    {
        _context = context;
    }

    // counts are done with COUNT subqueries in SQL instead of loading every member and task
    private static readonly Expression<Func<Project, ProjectDto>> ToDto = p => new ProjectDto(
        p.Id,
        p.Name,
        p.Description,
        p.OwnerId,
        p.Owner.FullName,
        p.Members.Count,
        p.Tasks.Count,
        p.Tasks.Count(t => t.Status != TaskItemStatus.Done),
        p.CreatedAt,
        p.UpdatedAt);

    public Task<Project?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return _context.Projects.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
    }

    public Task<ProjectDto?> GetDtoByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return _context.Projects
            .AsNoTracking()
            .Where(p => p.Id == id)
            .Select(ToDto)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<PagedResult<ProjectDto>> GetPagedAsync(ProjectQueryParameters query, int? memberUserId, CancellationToken cancellationToken = default)
    {
        var projects = _context.Projects.AsNoTracking();

        if (memberUserId.HasValue)
        {
            projects = projects.Where(p => p.Members.Any(m => m.UserId == memberUserId.Value));
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            projects = projects.Where(p => p.Name.Contains(search));
        }

        var totalCount = await projects.CountAsync(cancellationToken);

        var items = await projects
            .OrderByDescending(p => p.CreatedAt)
            .ThenBy(p => p.Id)
            .Skip(query.Skip)
            .Take(query.PageSize)
            .Select(ToDto)
            .ToListAsync(cancellationToken);

        return new PagedResult<ProjectDto>(items, totalCount, query.Page, query.PageSize);
    }

    public Task<bool> IsMemberAsync(int projectId, int userId, CancellationToken cancellationToken = default)
    {
        return _context.ProjectMembers.AnyAsync(m => m.ProjectId == projectId && m.UserId == userId, cancellationToken);
    }

    public Task<ProjectMember?> GetMemberAsync(int projectId, int userId, CancellationToken cancellationToken = default)
    {
        return _context.ProjectMembers.FirstOrDefaultAsync(m => m.ProjectId == projectId && m.UserId == userId, cancellationToken);
    }

    public Task<List<ProjectMemberDto>> GetMembersAsync(int projectId, CancellationToken cancellationToken = default)
    {
        return _context.ProjectMembers
            .AsNoTracking()
            .Where(m => m.ProjectId == projectId)
            .OrderBy(m => m.User.FullName)
            .Select(m => new ProjectMemberDto(m.UserId, m.User.FullName, m.User.Email, m.User.Role, m.JoinedAt))
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(Project project, CancellationToken cancellationToken = default)
    {
        await _context.Projects.AddAsync(project, cancellationToken);
    }

    public void Remove(Project project)
    {
        _context.Projects.Remove(project);
    }

    public async Task AddMemberAsync(ProjectMember member, CancellationToken cancellationToken = default)
    {
        await _context.ProjectMembers.AddAsync(member, cancellationToken);
    }

    public void RemoveMember(ProjectMember member)
    {
        _context.ProjectMembers.Remove(member);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return _context.SaveChangesAsync(cancellationToken);
    }
}
