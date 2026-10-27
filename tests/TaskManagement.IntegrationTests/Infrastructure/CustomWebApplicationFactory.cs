using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TaskManagement.Application.Auth;
using TaskManagement.Infrastructure.Persistence;

namespace TaskManagement.IntegrationTests.Infrastructure;

// Starts the real API in memory. SQL Server is swapped for an in-memory SQLite database
// so the tests don't need docker and every test run starts with an empty database.
public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    public const string AdminEmail = "admin@test.local";
    public const string AdminPassword = "Admin123!";

    // the in-memory database only lives as long as this connection is open.
    // Foreign Keys=True so cascade deletes / FK errors behave like SQL Server
    private readonly SqliteConnection _connection = new("DataSource=:memory:;Foreign Keys=True");
    private readonly SemaphoreSlim _adminTokenLock = new(1, 1);
    private string? _adminToken;

    public CustomWebApplicationFactory()
    {
        _connection.Open();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("Seed:AdminEmail", AdminEmail);
        builder.UseSetting("Seed:AdminPassword", AdminPassword);
        builder.UseSetting("Serilog:MinimumLevel:Default", "Warning");

        builder.ConfigureTestServices(services =>
        {
            // remove the SQL Server registration from AddInfrastructure
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<AppDbContext>>();

            services.AddDbContext<AppDbContext>(options => options.UseSqlite(_connection));
        });
    }

    public async Task<string> GetAdminTokenAsync()
    {
        await _adminTokenLock.WaitAsync();
        try
        {
            if (_adminToken is null)
            {
                var client = CreateClient();
                var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(AdminEmail, AdminPassword));
                response.EnsureSuccessStatusCode();
                var auth = await response.Content.ReadFromJsonAsync<AuthResponse>(TestJson.Options);
                _adminToken = auth!.AccessToken;
            }

            return _adminToken;
        }
        finally
        {
            _adminTokenLock.Release();
        }
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
            _connection.Dispose();
            _adminTokenLock.Dispose();
        }
    }
}
