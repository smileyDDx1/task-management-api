using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskManagement.Api.Authorization;
using TaskManagement.Application.Common.Models;
using TaskManagement.Application.Users;

namespace TaskManagement.Api.Controllers;

[ApiController]
[Route("api/users")]
[Authorize]
[Produces("application/json")]
public class UsersController : ControllerBase
{
    private readonly IUserService _userService;

    public UsersController(IUserService userService)
    {
        _userService = userService;
    }

    /// <summary>
    /// Lists users with paging. Managers need this to find people to add to their projects.
    /// </summary>
    [HttpGet]
    [Authorize(Policy = Policies.ManagerOrAdmin)]
    [ProducesResponseType<PagedResult<UserDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<UserDto>>> GetUsers([FromQuery] UserQueryParameters query, CancellationToken cancellationToken)
    {
        return Ok(await _userService.GetUsersAsync(query, cancellationToken));
    }

    /// <summary>
    /// Returns the profile of the logged in user.
    /// </summary>
    [HttpGet("me")]
    [ProducesResponseType<UserDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<UserDto>> GetMe(CancellationToken cancellationToken)
    {
        return Ok(await _userService.GetCurrentUserAsync(cancellationToken));
    }

    [HttpGet("{id:int}")]
    [Authorize(Policy = Policies.ManagerOrAdmin)]
    [ProducesResponseType<UserDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserDto>> GetById(int id, CancellationToken cancellationToken)
    {
        return Ok(await _userService.GetByIdAsync(id, cancellationToken));
    }

    /// <summary>
    /// Changes the role of a user (Admin only). The user has to log in again to get a token with the new role.
    /// </summary>
    [HttpPut("{id:int}/role")]
    [Authorize(Policy = Policies.AdminOnly)]
    [ProducesResponseType<UserDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserDto>> UpdateRole(int id, UpdateUserRoleRequest request, CancellationToken cancellationToken)
    {
        return Ok(await _userService.UpdateRoleAsync(id, request, cancellationToken));
    }

    /// <summary>
    /// Deactivates a user (soft delete). Deactivated users cannot log in.
    /// </summary>
    [HttpDelete("{id:int}")]
    [Authorize(Policy = Policies.AdminOnly)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Deactivate(int id, CancellationToken cancellationToken)
    {
        await _userService.DeactivateAsync(id, cancellationToken);
        return NoContent();
    }
}
