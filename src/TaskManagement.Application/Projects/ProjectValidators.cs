using FluentValidation;
using TaskManagement.Application.Common.Models;

namespace TaskManagement.Application.Projects;

public class CreateProjectRequestValidator : AbstractValidator<CreateProjectRequest>
{
    public CreateProjectRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Description).MaximumLength(1000);
    }
}

// same rules as create for now, kept separate in case update changes later
public class UpdateProjectRequestValidator : AbstractValidator<UpdateProjectRequest>
{
    public UpdateProjectRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Description).MaximumLength(1000);
    }
}

public class AddProjectMemberRequestValidator : AbstractValidator<AddProjectMemberRequest>
{
    public AddProjectMemberRequestValidator()
    {
        RuleFor(x => x.UserId).GreaterThan(0);
    }
}

public class ProjectQueryParametersValidator : AbstractValidator<ProjectQueryParameters>
{
    public ProjectQueryParametersValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, PaginationParameters.MaxPageSize);
        RuleFor(x => x.Search).MaximumLength(100);
    }
}
