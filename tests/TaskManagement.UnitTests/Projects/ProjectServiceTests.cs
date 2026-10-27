using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TaskManagement.Application.Common.Exceptions;
using TaskManagement.Application.Common.Interfaces;
using TaskManagement.Application.Common.Models;
using TaskManagement.Application.Projects;
using TaskManagement.Domain.Entities;
using TaskManagement.Domain.Enums;
using TaskManagement.UnitTests.Helpers;

namespace TaskManagement.UnitTests.Projects;

public class ProjectServiceTests
{
    private const int ManagerId = 10;
    private const int OtherManagerId = 11;

    private readonly IProjectRepository _projectRepository = Substitute.For<IProjectRepository>();
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly ITaskRepository _taskRepository = Substitute.For<ITaskRepository>();

    private ProjectService CreateService(int userId, UserRole role) =>
        new(_projectRepository, _userRepository, _taskRepository, TestUsers.CurrentUser(userId, role), NullLogger<ProjectService>.Instance);

    private static Project ProjectOwnedBy(int ownerId, int id = 1) => new() { Id = id, Name = "Website", OwnerId = ownerId };

    private static ProjectDto Dto(int id = 1) => new(id, "Website", null, ManagerId, "Manager", 1, 0, 0, DateTime.UtcNow, null);

    [Fact]
    public async Task GetProjectsAsync_AsAdmin_DoesNotFilterByMembership()
    {
        var query = new ProjectQueryParameters();
        var sut = CreateService(1, UserRole.Admin);

        await sut.GetProjectsAsync(query);

        await _projectRepository.Received(1).GetPagedAsync(query, null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetProjectsAsync_AsMember_OnlyReturnsOwnProjects()
    {
        var query = new ProjectQueryParameters();
        var sut = CreateService(20, UserRole.Member);

        await sut.GetProjectsAsync(query);

        await _projectRepository.Received(1).GetPagedAsync(query, 20, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetByIdAsync_ProjectDoesNotExist_ThrowsNotFound()
    {
        _projectRepository.GetDtoByIdAsync(5, Arg.Any<CancellationToken>()).Returns((ProjectDto?)null);
        var sut = CreateService(1, UserRole.Admin);

        await Assert.ThrowsAsync<NotFoundException>(() => sut.GetByIdAsync(5));
    }

    [Fact]
    public async Task GetByIdAsync_UserNotMember_ThrowsForbidden()
    {
        _projectRepository.GetDtoByIdAsync(1, Arg.Any<CancellationToken>()).Returns(Dto());
        _projectRepository.IsMemberAsync(1, 20, Arg.Any<CancellationToken>()).Returns(false);
        var sut = CreateService(20, UserRole.Member);

        await Assert.ThrowsAsync<ForbiddenException>(() => sut.GetByIdAsync(1));
    }

    [Fact]
    public async Task GetByIdAsync_UserIsMember_ReturnsProject()
    {
        _projectRepository.GetDtoByIdAsync(1, Arg.Any<CancellationToken>()).Returns(Dto());
        _projectRepository.IsMemberAsync(1, 20, Arg.Any<CancellationToken>()).Returns(true);
        var sut = CreateService(20, UserRole.Member);

        var result = await sut.GetByIdAsync(1);

        Assert.Equal("Website", result.Name);
    }

    [Fact]
    public async Task CreateAsync_SetsOwnerAndAddsOwnerAsMember()
    {
        _projectRepository.GetDtoByIdAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(Dto());
        var sut = CreateService(ManagerId, UserRole.Manager);

        await sut.CreateAsync(new CreateProjectRequest("  Website  ", "Company site"));

        await _projectRepository.Received(1).AddAsync(
            Arg.Is<Project>(p =>
                p.Name == "Website" &&
                p.OwnerId == ManagerId &&
                p.Members.Count == 1 &&
                p.Members.First().UserId == ManagerId),
            Arg.Any<CancellationToken>());
        await _projectRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateAsync_ManagerWhoIsNotOwner_ThrowsForbidden()
    {
        _projectRepository.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(ProjectOwnedBy(ManagerId));
        var sut = CreateService(OtherManagerId, UserRole.Manager);

        await Assert.ThrowsAsync<ForbiddenException>(() => sut.UpdateAsync(1, new UpdateProjectRequest("New name", null)));
        await _projectRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateAsync_AdminCanUpdateAnyProject()
    {
        var project = ProjectOwnedBy(ManagerId);
        _projectRepository.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(project);
        _projectRepository.GetDtoByIdAsync(1, Arg.Any<CancellationToken>()).Returns(Dto());
        var sut = CreateService(1, UserRole.Admin);

        await sut.UpdateAsync(1, new UpdateProjectRequest("New name", "desc"));

        Assert.Equal("New name", project.Name);
        await _projectRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteAsync_Owner_RemovesProject()
    {
        var project = ProjectOwnedBy(ManagerId);
        _projectRepository.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(project);
        var sut = CreateService(ManagerId, UserRole.Manager);

        await sut.DeleteAsync(1);

        _projectRepository.Received(1).Remove(project);
        await _projectRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteAsync_ProjectDoesNotExist_ThrowsNotFound()
    {
        _projectRepository.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns((Project?)null);
        var sut = CreateService(ManagerId, UserRole.Manager);

        await Assert.ThrowsAsync<NotFoundException>(() => sut.DeleteAsync(1));
    }

    [Fact]
    public async Task GetMembersAsync_Member_ReturnsMembers()
    {
        _projectRepository.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(ProjectOwnedBy(ManagerId));
        _projectRepository.IsMemberAsync(1, 20, Arg.Any<CancellationToken>()).Returns(true);
        _projectRepository.GetMembersAsync(1, Arg.Any<CancellationToken>())
            .Returns(new List<ProjectMemberDto> { new(20, "Bob", "bob@test.com", UserRole.Member, DateTime.UtcNow) });
        var sut = CreateService(20, UserRole.Member);

        var members = await sut.GetMembersAsync(1);

        Assert.Single(members);
    }

    [Fact]
    public async Task AddMemberAsync_ValidUser_AddsMembership()
    {
        _projectRepository.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(ProjectOwnedBy(ManagerId));
        _userRepository.GetByIdAsync(20, Arg.Any<CancellationToken>()).Returns(new User { Id = 20, IsActive = true });
        var sut = CreateService(ManagerId, UserRole.Manager);

        await sut.AddMemberAsync(1, new AddProjectMemberRequest(20));

        await _projectRepository.Received(1).AddMemberAsync(
            Arg.Is<ProjectMember>(m => m.ProjectId == 1 && m.UserId == 20), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AddMemberAsync_AlreadyMember_ThrowsConflict()
    {
        _projectRepository.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(ProjectOwnedBy(ManagerId));
        _userRepository.GetByIdAsync(20, Arg.Any<CancellationToken>()).Returns(new User { Id = 20, IsActive = true });
        _projectRepository.IsMemberAsync(1, 20, Arg.Any<CancellationToken>()).Returns(true);
        var sut = CreateService(ManagerId, UserRole.Manager);

        await Assert.ThrowsAsync<ConflictException>(() => sut.AddMemberAsync(1, new AddProjectMemberRequest(20)));
    }

    [Fact]
    public async Task AddMemberAsync_DeactivatedUser_ThrowsBadRequest()
    {
        _projectRepository.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(ProjectOwnedBy(ManagerId));
        _userRepository.GetByIdAsync(20, Arg.Any<CancellationToken>()).Returns(new User { Id = 20, IsActive = false });
        var sut = CreateService(ManagerId, UserRole.Manager);

        await Assert.ThrowsAsync<BadRequestException>(() => sut.AddMemberAsync(1, new AddProjectMemberRequest(20)));
    }

    [Fact]
    public async Task AddMemberAsync_UserDoesNotExist_ThrowsNotFound()
    {
        _projectRepository.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(ProjectOwnedBy(ManagerId));
        _userRepository.GetByIdAsync(20, Arg.Any<CancellationToken>()).Returns((User?)null);
        var sut = CreateService(ManagerId, UserRole.Manager);

        await Assert.ThrowsAsync<NotFoundException>(() => sut.AddMemberAsync(1, new AddProjectMemberRequest(20)));
    }

    [Fact]
    public async Task RemoveMemberAsync_Owner_ThrowsBadRequest()
    {
        _projectRepository.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(ProjectOwnedBy(ManagerId));
        var sut = CreateService(1, UserRole.Admin);

        await Assert.ThrowsAsync<BadRequestException>(() => sut.RemoveMemberAsync(1, ManagerId));
    }

    [Fact]
    public async Task RemoveMemberAsync_Member_RemovesAndUnassignsTheirTasks()
    {
        var membership = new ProjectMember { ProjectId = 1, UserId = 20 };
        _projectRepository.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(ProjectOwnedBy(ManagerId));
        _projectRepository.GetMemberAsync(1, 20, Arg.Any<CancellationToken>()).Returns(membership);
        var sut = CreateService(ManagerId, UserRole.Manager);

        await sut.RemoveMemberAsync(1, 20);

        _projectRepository.Received(1).RemoveMember(membership);
        await _taskRepository.Received(1).UnassignUserInProjectAsync(1, 20, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RemoveMemberAsync_NotAMember_ThrowsNotFound()
    {
        _projectRepository.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(ProjectOwnedBy(ManagerId));
        _projectRepository.GetMemberAsync(1, 20, Arg.Any<CancellationToken>()).Returns((ProjectMember?)null);
        var sut = CreateService(ManagerId, UserRole.Manager);

        await Assert.ThrowsAsync<NotFoundException>(() => sut.RemoveMemberAsync(1, 20));
    }
}
