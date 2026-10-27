using TaskManagement.Application.Common.Models;
using TaskManagement.Domain.Enums;

namespace TaskManagement.Application.Users;

public record UserDto(
    int Id,
    string FullName,
    string Email,
    UserRole Role,
    bool IsActive,
    DateTime CreatedAt);

public record UpdateUserRoleRequest(UserRole Role);

public class UserQueryParameters : PaginationParameters
{
    // searches in name and email
    public string? Search { get; set; }
    public UserRole? Role { get; set; }
    public bool? IsActive { get; set; }
}
