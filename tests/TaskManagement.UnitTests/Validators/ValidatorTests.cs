using FluentValidation.TestHelper;
using TaskManagement.Application.Auth;
using TaskManagement.Application.Projects;
using TaskManagement.Application.Tasks;
using TaskManagement.Application.Users;
using TaskManagement.Domain.Enums;

namespace TaskManagement.UnitTests.Validators;

public class ValidatorTests
{
    [Fact]
    public void RegisterRequest_Valid_HasNoErrors()
    {
        var result = new RegisterRequestValidator().TestValidate(new RegisterRequest("Jane Doe", "jane@test.com", "Password1"));

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("short1A")]       // too short
    [InlineData("alllowercase1")] // no uppercase
    [InlineData("ALLUPPERCASE1")] // no lowercase
    [InlineData("NoNumbersHere")] // no digit
    public void RegisterRequest_WeakPassword_HasError(string password)
    {
        var result = new RegisterRequestValidator().TestValidate(new RegisterRequest("Jane", "jane@test.com", password));

        result.ShouldHaveValidationErrorFor(x => x.Password);
    }

    [Fact]
    public void RegisterRequest_InvalidEmail_HasError()
    {
        var result = new RegisterRequestValidator().TestValidate(new RegisterRequest("Jane", "not-an-email", "Password1"));

        result.ShouldHaveValidationErrorFor(x => x.Email);
    }

    [Fact]
    public void LoginRequest_Empty_HasErrors()
    {
        var result = new LoginRequestValidator().TestValidate(new LoginRequest("", ""));

        result.ShouldHaveValidationErrorFor(x => x.Email);
        result.ShouldHaveValidationErrorFor(x => x.Password);
    }

    [Fact]
    public void CreateProjectRequest_EmptyName_HasError()
    {
        var result = new CreateProjectRequestValidator().TestValidate(new CreateProjectRequest("", null));

        result.ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void UpdateProjectRequest_NameTooLong_HasError()
    {
        var result = new UpdateProjectRequestValidator().TestValidate(new UpdateProjectRequest(new string('a', 101), null));

        result.ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void CreateTaskRequest_DueDateInThePast_HasError()
    {
        var request = new CreateTaskRequest(1, "Task", null, TaskPriority.Low, DateTime.UtcNow.AddDays(-5), null);

        var result = new CreateTaskRequestValidator().TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.DueDate);
    }

    [Fact]
    public void CreateTaskRequest_InvalidPriority_HasError()
    {
        var request = new CreateTaskRequest(1, "Task", null, (TaskPriority)42, null, null);

        var result = new CreateTaskRequestValidator().TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Priority);
    }

    [Fact]
    public void UpdateTaskStatusRequest_InvalidStatus_HasError()
    {
        var result = new UpdateTaskStatusRequestValidator().TestValidate(new UpdateTaskStatusRequest((TaskItemStatus)9));

        result.ShouldHaveValidationErrorFor(x => x.Status);
    }

    [Fact]
    public void TaskQueryParameters_Defaults_AreValid()
    {
        new TaskQueryParametersValidator().TestValidate(new TaskQueryParameters()).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void TaskQueryParameters_PageSizeOverMax_HasError()
    {
        var result = new TaskQueryParametersValidator().TestValidate(new TaskQueryParameters { PageSize = 500 });

        result.ShouldHaveValidationErrorFor(x => x.PageSize);
    }

    [Fact]
    public void TaskQueryParameters_UnknownSortField_HasError()
    {
        var result = new TaskQueryParametersValidator().TestValidate(new TaskQueryParameters { SortBy = "password" });

        result.ShouldHaveValidationErrorFor(x => x.SortBy);
    }

    [Fact]
    public void TaskQueryParameters_DueToBeforeDueFrom_HasError()
    {
        var query = new TaskQueryParameters { DueFrom = new DateTime(2026, 5, 10), DueTo = new DateTime(2026, 5, 1) };

        var result = new TaskQueryParametersValidator().TestValidate(query);

        result.ShouldHaveValidationErrorFor(x => x.DueTo);
    }

    [Fact]
    public void UserQueryParameters_PageZero_HasError()
    {
        var result = new UserQueryParametersValidator().TestValidate(new UserQueryParameters { Page = 0 });

        result.ShouldHaveValidationErrorFor(x => x.Page);
    }

    [Fact]
    public void AssignTaskRequest_NullAssignee_IsValid()
    {
        new AssignTaskRequestValidator().TestValidate(new AssignTaskRequest(null)).ShouldNotHaveAnyValidationErrors();
    }
}
