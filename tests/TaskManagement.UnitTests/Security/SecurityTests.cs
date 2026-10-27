using System.IdentityModel.Tokens.Jwt;
using Microsoft.Extensions.Options;
using TaskManagement.Domain.Entities;
using TaskManagement.Domain.Enums;
using TaskManagement.Infrastructure.Security;

namespace TaskManagement.UnitTests.Security;

public class SecurityTests
{
    [Fact]
    public void JwtTokenGenerator_TokenContainsUserClaims()
    {
        var settings = new JwtSettings
        {
            Issuer = "test-issuer",
            Audience = "test-audience",
            Secret = "a-very-long-secret-key-just-for-unit-tests-123",
            ExpiryMinutes = 30
        };
        var generator = new JwtTokenGenerator(Options.Create(settings));
        var user = new User { Id = 12, Email = "jane@test.com", FullName = "Jane Doe", Role = UserRole.Manager };

        var result = generator.GenerateToken(user);
        var token = new JwtSecurityTokenHandler().ReadJwtToken(result.Token);

        Assert.Equal("test-issuer", token.Issuer);
        Assert.Contains("test-audience", token.Audiences);
        Assert.Equal("12", token.Subject);
        Assert.Contains(token.Claims, c => c.Type == "role" && c.Value == "Manager");
        Assert.InRange(result.ExpiresAt, DateTime.UtcNow.AddMinutes(29), DateTime.UtcNow.AddMinutes(31));
    }

    [Fact]
    public void BCryptPasswordHasher_HashAndVerify()
    {
        var hasher = new BCryptPasswordHasher();

        var hash = hasher.Hash("Password1");

        Assert.NotEqual("Password1", hash);
        Assert.True(hasher.Verify("Password1", hash));
        Assert.False(hasher.Verify("password1", hash));
    }
}
