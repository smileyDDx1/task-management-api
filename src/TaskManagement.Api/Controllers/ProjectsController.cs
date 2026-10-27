using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskManagement.Api.Authorization;
using TaskManagement.Application.Common.Models;
using TaskManagement.Application.Projects;

namespace TaskManagement.Api.Controllers;

[ApiController]
[Route("api/projects")]
[Authorize]
[Produces("application/json")]
public class ProjectsController : ControllerBase
{
    private readonly IProjectService _projectService;

    public ProjectsController(IProjectService projectService)
    {
        _projectService = projectService;
    }

    /// <summary>
    /// Lists projects. Admins see all projects, other users only the ones they are a member of.
    /// </summary>
    [HttpGet]
    [ProducesResponseType<PagedResult<ProjectDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<ProjectDto>>> GetProjects([FromQuery] ProjectQueryParameters query, CancellationToken cancellationToken)
    {
        return Ok(await _projectService.GetProjectsAsync(query, cancellationToken));
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType<ProjectDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProjectDto>> GetById(int id, CancellationToken cancellationToken)
    {
        return Ok(await _projectService.GetByIdAsync(id, cancellationToken));
    }

    /// <summary>
    /// Creates a project. The creator becomes the owner and first member.
    /// </summary>
    [HttpPost]
    [Authorize(Policy = Policies.ManagerOrAdmin)]
    [ProducesResponseType<ProjectDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ProjectDto>> Create(CreateProjectRequest request, CancellationToken cancellationToken)
    {
        var project = await _projectService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = project.Id }, project);
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = Policies.ManagerOrAdmin)]
    [ProducesResponseType<ProjectDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProjectDto>> Update(int id, UpdateProjectRequest request, CancellationToken cancellationToken)
    {
        return Ok(await _projectService.UpdateAsync(id, request, cancellationToken));
    }

    /// <summary>
    /// Deletes a project together with all of its tasks.
    /// </summary>
    [HttpDelete("{id:int}")]
    [Authorize(Policy = Policies.ManagerOrAdmin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await _projectService.DeleteAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpGet("{id:int}/members")]
    [ProducesResponseType<List<ProjectMemberDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<List<ProjectMemberDto>>> GetMembers(int id, CancellationToken cancellationToken)
    {
        return Ok(await _projectService.GetMembersAsync(id, cancellationToken));
    }

    [HttpPost("{id:int}/members")]
    [Authorize(Policy = Policies.ManagerOrAdmin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AddMember(int id, AddProjectMemberRequest request, CancellationToken cancellationToken)
    {
        await _projectService.AddMemberAsync(id, request, cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Removes a member from the project. Their tasks in this project become unassigned.
    /// </summary>
    [HttpDelete("{id:int}/members/{userId:int}")]
    [Authorize(Policy = Policies.ManagerOrAdmin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveMember(int id, int userId, CancellationToken cancellationToken)
    {
        await _projectService.RemoveMemberAsync(id, userId, cancellationToken);
        return NoContent();
    }
}
