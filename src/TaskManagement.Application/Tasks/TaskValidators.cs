using FluentValidation;
using TaskManagement.Application.Common.Models;

namespace TaskManagement.Application.Tasks;

public class CreateTaskRequestValidator : AbstractValidator<CreateTaskRequest>
{
    public CreateTaskRequestValidator()
    {
        RuleFor(x => x.ProjectId).GreaterThan(0);
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(2000);
        RuleFor(x => x.Priority).IsInEnum();

        // a new task with a due date in the past doesn't make sense
        RuleFor(x => x.DueDate)
            .GreaterThan(_ => DateTime.UtcNow.Date.AddDays(-1))
            .When(x => x.DueDate.HasValue)
            .WithMessage("Due date cannot be in the past.");

        RuleFor(x => x.AssigneeId).GreaterThan(0).When(x => x.AssigneeId.HasValue);
    }
}

public class UpdateTaskRequestValidator : AbstractValidator<UpdateTaskRequest>
{
    public UpdateTaskRequestValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(2000);
        RuleFor(x => x.Priority).IsInEnum();
        // no past date check here, an existing task can already be overdue
    }
}

public class UpdateTaskStatusRequestValidator : AbstractValidator<UpdateTaskStatusRequest>
{
    public UpdateTaskStatusRequestValidator()
    {
        RuleFor(x => x.Status).IsInEnum();
    }
}

public class AssignTaskRequestValidator : AbstractValidator<AssignTaskRequest>
{
    public AssignTaskRequestValidator()
    {
        RuleFor(x => x.AssigneeId).GreaterThan(0).When(x => x.AssigneeId.HasValue);
    }
}

public class TaskQueryParametersValidator : AbstractValidator<TaskQueryParameters>
{
    public TaskQueryParametersValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, PaginationParameters.MaxPageSize);
        RuleFor(x => x.Status).IsInEnum().When(x => x.Status.HasValue);
        RuleFor(x => x.Priority).IsInEnum().When(x => x.Priority.HasValue);
        RuleFor(x => x.Search).MaximumLength(100);

        RuleFor(x => x.DueTo)
            .GreaterThanOrEqualTo(x => x.DueFrom)
            .When(x => x.DueFrom.HasValue && x.DueTo.HasValue)
            .WithMessage("DueTo must be after DueFrom.");

        RuleFor(x => x.SortBy)
            .Must(s => TaskQueryParameters.AllowedSortFields.Contains(s, StringComparer.OrdinalIgnoreCase))
            .WithMessage($"SortBy must be one of: {string.Join(", ", TaskQueryParameters.AllowedSortFields)}");

        RuleFor(x => x.SortDirection)
            .Must(s => s.Equals("asc", StringComparison.OrdinalIgnoreCase) || s.Equals("desc", StringComparison.OrdinalIgnoreCase))
            .WithMessage("SortDirection must be 'asc' or 'desc'.");
    }
}
