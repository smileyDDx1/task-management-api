using Microsoft.Extensions.Logging;
using TaskManagement.Application.Common.Exceptions;
using TaskManagement.Application.Common.Interfaces;
using TaskManagement.Application.Common.Models;
using TaskManagement.Domain.Entities;
using TaskManagement.Domain.Enums;

namespace TaskManagement.Application.Projects;

public class ProjectService : IProjectService
{
    private readonly IProjectRepository _projectRepository;
    private readonly IUserRepository _userRepository;
    private readonly ITaskRepository _taskRepository;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<ProjectService> _logger;

    public ProjectService(
        IProjectRepository projectRepository,
        IUserRepository userRepository,
        ITaskRepository taskRepository,
        ICurrentUserService currentUser,
        ILogger<ProjectService> logger)
    {
        _projectRepository = projectRepository;
        _userRepository = userRepository;
        _taskRepository = taskRepository;
        _currentUser = currentUser;
        _logger = logger;
    }

    private bool IsAdmin => _currentUser.Role == UserRole.Admin;

    public Task<PagedResult<ProjectDto>> GetProjectsAsync(ProjectQueryParameters query, CancellationToken cancellationToken = default)
    {
        // admins see every project, everyone else only sees projects they are part of
        int? memberFilter = IsAdmin ? null : _currentUser.UserId;
        return _projectRepository.GetPagedAsync(query, memberFilter, cancellationToken);
    }

    public async Task<ProjectDto> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var project = await _projectRepository.GetDtoByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException("Project", id);

        await EnsureCanViewAsync(id, cancellationToken);

        return project;
    }

    public async Task<ProjectDto> CreateAsync(CreateProjectRequest request, CancellationToken cancellationToken = default)
    {
        var project = new Project
        {
            Name = request.Name.Trim(),
            Description = request.Description?.Trim(),
            OwnerId = _currentUser.UserId
        };

        // the person who creates the project is automatically a member of it
        project.Members.Add(new ProjectMember { UserId = _currentUser.UserId });

        await _projectRepository.AddAsync(project, cancellationToken);
        await _projectRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Project {ProjectId} created by user {UserId}", project.Id, _currentUser.UserId);

        return (await _projectRepository.GetDtoByIdAsync(project.Id, cancellationToken))!;
    }

    public async Task<ProjectDto> UpdateAsync(int id, UpdateProjectRequest request, CancellationToken cancellationToken = default)
    {
        var project = await GetProjectForManagementAsync(id, cancellationToken);

        project.Name = request.Name.Trim();
        project.Description = request.Description?.Trim();
        await _projectRepository.SaveChangesAsync(cancellationToken);

        return (await _projectRepository.GetDtoByIdAsync(id, cancellationToken))!;
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var project = await GetProjectForManagementAsync(id, cancellationToken);

        // tasks and memberships are removed by cascade delete (see ProjectConfiguration)
        _projectRepository.Remove(project);
        await _projectRepository.SaveChangesAsync(cancellationToken);

        _logger.LogWarning("Project {ProjectId} deleted by user {UserId}", id, _currentUser.UserId);
    }

    public async Task<List<ProjectMemberDto>> GetMembersAsync(int projectId, CancellationToken cancellationToken = default)
    {
        _ = await _projectRepository.GetByIdAsync(projectId, cancellationToken)
            ?? throw new NotFoundException("Project", projectId);

        await EnsureCanViewAsync(projectId, cancellationToken);

        return await _projectRepository.GetMembersAsync(projectId, cancellationToken);
    }

    public async Task AddMemberAsync(int projectId, AddProjectMemberRequest request, CancellationToken cancellationToken = default)
    {
        await GetProjectForManagementAsync(projectId, cancellationToken);

        var user = await _userRepository.GetByIdAsync(request.UserId, cancellationToken)
            ?? throw new NotFoundException("User", request.UserId);

        if (!user.IsActive)
        {
            throw new BadRequestException("Cannot add a deactivated user to a project.");
        }

        if (await _projectRepository.IsMemberAsync(projectId, user.Id, cancellationToken))
        {
            throw new ConflictException("User is already a member of this project.");
        }

        await _projectRepository.AddMemberAsync(new ProjectMember { ProjectId = projectId, UserId = user.Id }, cancellationToken);
        await _projectRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("User {MemberId} added to project {ProjectId}", user.Id, projectId);
    }

    public async Task RemoveMemberAsync(int projectId, int userId, CancellationToken cancellationToken = default)
    {
        var project = await GetProjectForManagementAsync(projectId, cancellationToken);

        if (project.IsOwnedBy(userId))
        {
            throw new BadRequestException("The project owner cannot be removed from the project.");
        }

        var member = await _projectRepository.GetMemberAsync(projectId, userId, cancellationToken)
            ?? throw new NotFoundException("Project member", userId);

        _projectRepository.RemoveMember(member);
        await _projectRepository.SaveChangesAsync(cancellationToken);

        var unassigned = await _taskRepository.UnassignUserInProjectAsync(projectId, userId, cancellationToken);

        _logger.LogInformation("User {MemberId} removed from project {ProjectId}, {TaskCount} tasks unassigned",
            userId, projectId, unassigned);
    }

    // loads the project and checks that the current user is allowed to change it (admin or owner)
    private async Task<Project> GetProjectForManagementAsync(int projectId, CancellationToken cancellationToken)
    {
        var project = await _projectRepository.GetByIdAsync(projectId, cancellationToken)
            ?? throw new NotFoundException("Project", projectId);

        if (!IsAdmin && !project.IsOwnedBy(_currentUser.UserId))
        {
            throw new ForbiddenException("Only the project owner or an admin can manage this project.");
        }

        return project;
    }

    private async Task EnsureCanViewAsync(int projectId, CancellationToken cancellationToken)
    {
        if (IsAdmin)
            return;

        if (!await _projectRepository.IsMemberAsync(projectId, _currentUser.UserId, cancellationToken))
        {
            throw new ForbiddenException("You are not a member of this project.");
        }
    }
}
