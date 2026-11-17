using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace ReturnFlow.IntegrationTests;

public sealed class HealthEndpointTests : IClassFixture<ReturnFlowApiFactory>
{
    private readonly HttpClient _client;

    public HealthEndpointTests(ReturnFlowApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Health_returns_minimal_safe_status()
    {
        var response = await _client.GetAsync("/health");
        var payload = await response.Content.ReadFromJsonAsync<HealthPayload>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(payload);
        Assert.Equal("Healthy", payload.Status);
        Assert.Equal("ReturnFlow.Api", payload.Service);
    }

    private sealed record HealthPayload(string Status, string Service);
}
