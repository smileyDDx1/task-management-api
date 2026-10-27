namespace TaskManagement.Infrastructure.Persistence.Seed;

public class SeedSettings
{
    public const string SectionName = "Seed";

    public string AdminEmail { get; set; } = "admin@taskmanager.local";
    public string AdminPassword { get; set; } = string.Empty;
    public string AdminName { get; set; } = "System Admin";

    // fake projects/tasks so the list endpoints have something to page through
    public bool DemoData { get; set; }
    public int DemoTaskCount { get; set; } = 20000;
}
