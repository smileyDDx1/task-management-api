using NSubstitute;
using TaskManagement.Application.Common.Interfaces;
using TaskManagement.Domain.Enums;

namespace TaskManagement.UnitTests.Helpers;

public static class TestUsers
{
    public static ICurrentUserService CurrentUser(int id, UserRole role)
    {
        var currentUser = Substitute.For<ICurrentUserService>();
        currentUser.UserId.Returns(id);
        currentUser.Role.Returns(role);
        return currentUser;
    }
}
