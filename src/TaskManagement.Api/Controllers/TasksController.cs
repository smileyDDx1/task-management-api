using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskManagement.Api.Authorization;
using TaskManagement.Application.Common.Models;
using TaskManagement.Application.Tasks;

namespace TaskManagement.Api.Controllers;

[ApiController]
[Route("api/tasks")]
[Authorize]
[Produces("application/json")]
public class TasksController : ControllerBase
{
    private readonly ITaskService _taskService;

    public TasksController(ITaskService taskService)
    {
        _taskService = taskService;
    }

    /// <summary>
    /// Lists tasks with filtering, sorting and paging.
    /// </summary>
    /// <remarks>
    /// Example: GET /api/tasks?projectId=1&amp;status=InProgress&amp;sortBy=dueDate&amp;sortDirection=asc&amp;page=1&amp;pageSize=20
    /// </remarks>
    [HttpGet]
    [ProducesResponseType<PagedResult<TaskDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResult<TaskDto>>> GetTasks([FromQuery] TaskQueryParameters query, CancellationToken cancellationToken)
    {
        return Ok(await _taskService.GetTasksAsync(query, cancellationToken));
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType<TaskDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TaskDto>> GetById(int id, CancellationToken cancellationToken)
    {
        return Ok(await _taskService.GetByIdAsync(id, cancellationToken));
    }

    [HttpPost]
    [Authorize(Policy = Policies.ManagerOrAdmin)]
    [ProducesResponseType<TaskDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TaskDto>> Create(CreateTaskRequest request, CancellationToken cancellationToken)
    {
        var task = await _taskService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = task.Id }, task);
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = Policies.ManagerOrAdmin)]
    [ProducesResponseType<TaskDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TaskDto>> Update(int id, UpdateTaskRequest request, CancellationToken cancellationToken)
    {
        return Ok(await _taskService.UpdateAsync(id, request, cancellationToken));
    }

    /// <summary>
    /// Moves a task to another status. Members can only change tasks assigned to them.
    /// </summary>
    [HttpPatch("{id:int}/status")]
    [ProducesResponseType<TaskDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TaskDto>> UpdateStatus(int id, UpdateTaskStatusRequest request, CancellationToken cancellationToken)
    {
        return Ok(await _taskService.UpdateStatusAsync(id, request, cancellationToken));
    }

    /// <summary>
    /// Assigns the task to a project member, or unassigns it when assigneeId is null.
    /// </summary>
    [HttpPatch("{id:int}/assignee")]
    [Authorize(Policy = Policies.ManagerOrAdmin)]
    [ProducesResponseType<TaskDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TaskDto>> Assign(int id, AssignTaskRequest request, CancellationToken cancellationToken)
    {
        return Ok(await _taskService.AssignAsync(id, request, cancellationToken));
    }

    [HttpDelete("{id:int}")]
    [Authorize(Policy = Policies.ManagerOrAdmin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await _taskService.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
