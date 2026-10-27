using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TaskManagement.Application.Common.Interfaces;
using TaskManagement.Domain.Entities;
using TaskManagement.Domain.Enums;

namespace TaskManagement.Infrastructure.Persistence.Seed;

public class DbInitializer
{
    private readonly AppDbContext _context;
    private readonly IPasswordHasher _passwordHasher;
    private readonly SeedSettings _settings;
    private readonly ILogger<DbInitializer> _logger;

    public DbInitializer(
        AppDbContext context,
        IPasswordHasher passwordHasher,
        IOptions<SeedSettings> settings,
        ILogger<DbInitializer> logger)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        // migrations are written for SQL Server, the integration tests use SQLite so they just create the schema
        if (_context.Database.IsSqlServer())
        {
            _logger.LogInformation("Applying database migrations");
            await _context.Database.MigrateAsync(cancellationToken);
        }
        else
        {
            await _context.Database.EnsureCreatedAsync(cancellationToken);
        }

        await SeedAdminAsync(cancellationToken);

        if (_settings.DemoData)
        {
            await new DemoDataSeeder(_context, _passwordHasher, _logger).SeedAsync(_settings.DemoTaskCount, cancellationToken);
        }
    }

    private async Task SeedAdminAsync(CancellationToken cancellationToken)
    {
        if (await _context.Users.AnyAsync(u => u.Role == UserRole.Admin, cancellationToken))
            return;

        if (string.IsNullOrWhiteSpace(_settings.AdminPassword))
        {
            _logger.LogWarning("No admin user exists and Seed:AdminPassword is not set, skipping admin seed");
            return;
        }

        _context.Users.Add(new User
        {
            FullName = _settings.AdminName,
            Email = _settings.AdminEmail.ToLowerInvariant(),
            PasswordHash = _passwordHasher.Hash(_settings.AdminPassword),
            Role = UserRole.Admin
        });

        await _context.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Seeded admin user {Email}", _settings.AdminEmail);
    }
}
