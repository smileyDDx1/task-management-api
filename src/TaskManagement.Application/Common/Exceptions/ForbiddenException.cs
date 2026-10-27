namespace TaskManagement.Application.Common.Exceptions;

// the user is logged in but is not allowed to touch this resource -> 403
public class ForbiddenException : Exception
{
    public ForbiddenException(string message = "You do not have permission to perform this action.")
        : base(message)
    {
    }
}
