namespace TaskManagement.Application.Common.Exceptions;

// business rule errors that FluentValidation can't check on its own
// (e.g. assigning a task to someone who is not in the project) -> 400
public class BadRequestException : Exception
{
    public BadRequestException(string message) : base(message)
    {
    }
}
