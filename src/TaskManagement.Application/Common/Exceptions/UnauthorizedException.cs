namespace TaskManagement.Application.Common.Exceptions;

// wrong credentials / inactive account -> 401
public class UnauthorizedException : Exception
{
    public UnauthorizedException(string message) : base(message)
    {
    }
}
