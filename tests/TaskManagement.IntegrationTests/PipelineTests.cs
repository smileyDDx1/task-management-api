using System.Net;
using TaskManagement.IntegrationTests.Infrastructure;

namespace TaskManagement.IntegrationTests;

// tests for the middleware / infrastructure parts that are not tied to one controller
public class PipelineTests : ApiTestBase
{
    public PipelineTests(CustomWebApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task HealthCheck_ReturnsHealthy()
    {
        var response = await CreateClient().GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Response_EchoesCorrelationIdHeader()
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add("X-Correlation-ID", "my-test-id-123");

        var response = await client.GetAsync("/health");

        Assert.Equal("my-test-id-123", response.Headers.GetValues("X-Correlation-ID").Single());
    }

    [Fact]
    public async Task Response_GeneratesCorrelationIdWhenMissing()
    {
        var response = await CreateClient().GetAsync("/health");

        Assert.True(response.Headers.TryGetValues("X-Correlation-ID", out var values));
        Assert.False(string.IsNullOrWhiteSpace(values.Single()));
    }

    [Fact]
    public async Task UnknownRoute_Returns404()
    {
        var response = await CreateClient().GetAsync("/api/does-not-exist");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
