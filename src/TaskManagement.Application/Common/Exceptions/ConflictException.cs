namespace TaskManagement.Application.Common.Exceptions;

// for things like "email already registered" or "user is already a member" -> 409
public class ConflictException : Exception
{
    public ConflictException(string message) : base(message)
    {
    }
}
