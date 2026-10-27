using TaskManagement.Application.Common.Interfaces;

namespace TaskManagement.Infrastructure.Security;

public class BCryptPasswordHasher : IPasswordHasher
{
    // bcrypt adds the salt to the hash for us, so there is no separate salt column
    public string Hash(string password) => BCrypt.Net.BCrypt.HashPassword(password);

    public bool Verify(string password, string passwordHash) => BCrypt.Net.BCrypt.Verify(password, passwordHash);
}
