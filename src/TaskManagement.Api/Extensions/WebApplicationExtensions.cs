using TaskManagement.Infrastructure.Persistence.Seed;

namespace TaskManagement.Api.Extensions;

public static class WebApplicationExtensions
{
    // runs migrations + seeds the admin user when the app starts
    public static async Task InitialiseDatabaseAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var initializer = scope.ServiceProvider.GetRequiredService<DbInitializer>();
        await initializer.InitializeAsync();
    }
}
