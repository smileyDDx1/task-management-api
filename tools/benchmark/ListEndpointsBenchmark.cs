#:property PublishAot=false

// Small benchmark I use to measure the list endpoints before/after query changes.
// Runs as a .NET 10 file-based app:
//   dotnet run tools/benchmark/ListEndpointsBenchmark.cs -- http://localhost:5000 200
// Needs the API running with Seed__DemoData=true so there is enough data.

using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

var baseUrl = args.Length > 0 ? args[0].TrimEnd('/') : "http://localhost:5000";
var iterations = args.Length > 1 ? int.Parse(args[1]) : 200;
// optional: only run scenarios whose url contains this text, e.g. "/api/projects"
var only = args.Length > 2 ? args[2] : null;
const int warmup = 10;
const int concurrency = 10;

using var http = new HttpClient { BaseAddress = new Uri(baseUrl), Timeout = TimeSpan.FromMinutes(2) };

var adminToken = await LoginAsync("admin@taskmanager.local", "Admin123!");
var memberToken = await LoginAsync("member1@demo.local", "Password123!");

var scenarios = new (string Name, string Url, string Token)[]
{
    ("GET /api/tasks (admin, page 1 of all tasks)", "/api/tasks?page=1&pageSize=20", adminToken),
    ("GET /api/tasks (admin, status + sort by due date)", "/api/tasks?page=1&pageSize=20&status=InProgress&sortBy=dueDate&sortDirection=asc", adminToken),
    ("GET /api/tasks (member, only own projects)", "/api/tasks?page=1&pageSize=20", memberToken),
    ("GET /api/projects (admin)", "/api/projects?page=1&pageSize=10", adminToken),
};

Console.WriteLine($"Target: {baseUrl}, {iterations} sequential requests per endpoint (+{warmup} warm-up), then {concurrency} concurrent clients");
Console.WriteLine();
Console.WriteLine("| Endpoint | Total rows | Mean (ms) | p50 (ms) | p95 (ms) | p99 (ms) | Throughput @10 clients (req/s) |");
Console.WriteLine("|---|---|---|---|---|---|---|");

foreach (var (name, url, token) in scenarios)
{
    if (only is not null && !url.Contains(only, StringComparison.OrdinalIgnoreCase))
        continue;

    var totalCount = 0;
    for (var i = 0; i < warmup; i++)
    {
        totalCount = await SendAsync(url, token);
    }

    var timings = new List<double>(iterations);
    for (var i = 0; i < iterations; i++)
    {
        var sw = Stopwatch.StartNew();
        await SendAsync(url, token);
        timings.Add(sw.Elapsed.TotalMilliseconds);
    }

    var (throughput, failed) = await MeasureThroughputAsync(url, token, iterations);
    var throughputText = throughput < 10 ? $"{throughput:F1}" : $"{throughput:F0}";
    if (failed > 0)
        throughputText += $" ({failed}/{iterations} timed out)";

    timings.Sort();
    Console.WriteLine($"| {name} | {totalCount} | {timings.Average():F1} | {Percentile(timings, 50):F1} | {Percentile(timings, 95):F1} | {Percentile(timings, 99):F1} | {throughputText} |");
}

async Task<string> LoginAsync(string email, string password)
{
    var response = await http.PostAsJsonAsync("/api/auth/login", new { email, password });
    response.EnsureSuccessStatusCode();
    using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    return json.RootElement.GetProperty("accessToken").GetString()!;
}

async Task<int> SendAsync(string url, string token)
{
    using var request = new HttpRequestMessage(HttpMethod.Get, url);
    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
    using var response = await http.SendAsync(request);
    response.EnsureSuccessStatusCode();
    using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    return json.RootElement.GetProperty("totalCount").GetInt32();
}

// successful requests per second with 10 clients at the same time.
// requests slower than the HttpClient timeout (2 min) are counted as failed instead of crashing the run
async Task<(double RequestsPerSecond, int Failed)> MeasureThroughputAsync(string url, string token, int totalRequests)
{
    var remaining = totalRequests;
    var failed = 0;
    var sw = Stopwatch.StartNew();
    var workers = Enumerable.Range(0, concurrency).Select(async _ =>
    {
        while (Interlocked.Decrement(ref remaining) >= 0)
        {
            try
            {
                await SendAsync(url, token);
            }
            catch (Exception ex) when (ex is TaskCanceledException or HttpRequestException)
            {
                Interlocked.Increment(ref failed);
            }
        }
    });
    await Task.WhenAll(workers);
    return ((totalRequests - failed) / sw.Elapsed.TotalSeconds, failed);
}

static double Percentile(List<double> sorted, int percentile)
{
    var index = (int)Math.Ceiling(percentile / 100.0 * sorted.Count) - 1;
    return sorted[Math.Clamp(index, 0, sorted.Count - 1)];
}
