using System.Text.Json;
using System.Text.Json.Serialization;

namespace TaskManagement.IntegrationTests.Infrastructure;

public static class TestJson
{
    // same settings as the API (camelCase + enums as strings)
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };
}
