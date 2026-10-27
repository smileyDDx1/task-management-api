using FluentValidation;
using TaskManagement.Application.Common.Models;

namespace TaskManagement.Application.Users;

public class UpdateUserRoleRequestValidator : AbstractValidator<UpdateUserRoleRequest>
{
    public UpdateUserRoleRequestValidator()
    {
        RuleFor(x => x.Role).IsInEnum();
    }
}

public class UserQueryParametersValidator : AbstractValidator<UserQueryParameters>
{
    public UserQueryParametersValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, PaginationParameters.MaxPageSize);
        RuleFor(x => x.Search).MaximumLength(100);
        RuleFor(x => x.Role).IsInEnum().When(x => x.Role.HasValue);
    }
}
