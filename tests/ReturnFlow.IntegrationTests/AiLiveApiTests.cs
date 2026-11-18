using System.Net.Http.Json;
using ReturnFlow.Application;
using Xunit;

namespace ReturnFlow.IntegrationTests;

public sealed class AiLiveApiTests : IClassFixture<ReturnFlowApiFactory>
{
    private readonly HttpClient _client;

    public AiLiveApiTests(ReturnFlowApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Live_is_disabled_by_default_and_cannot_change_business_state()
    {
        var status = await _client.GetFromJsonAsync<LiveStatus>("/api/demo/live-status");
        var sessionResponse = await _client.PostAsJsonAsync(
            "/api/demo/sessions",
            new { scenarioId = "S01" });
        var session = (await sessionResponse.Content.ReadFromJsonAsync<DemoSessionSnapshot>())!;
        var before = session.Order.Items.Single().AvailableQuantity;

        var response = await _client.PostAsJsonAsync(
            $"/api/demo/sessions/{session.SessionId}/ai-runs",
            new
            {
                selectedOrderId = session.Order.Id,
                submissionKey = "live-disabled-test",
                message = "I want to return one more lamp.",
                mode = "Live",
            });
        var trace = (await response.Content.ReadFromJsonAsync<AiExecutionTrace>())!;
        var after = (await _client.GetFromJsonAsync<DemoOrderSummary>(
            $"/api/demo/sessions/{session.SessionId}/orders/{session.Order.Id}"))!;

        Assert.NotNull(status);
        Assert.False(status.Enabled);
        Assert.Equal("Live", trace.Mode);
        Assert.Equal("TechnicalFailure", trace.FinalOutcome);
        Assert.Equal("Not committed", trace.CommitStatus);
        Assert.Null(trace.CreatedReturnCaseId);
        Assert.Contains(
            "LIVE_MODE_DISABLED",
            trace.Stages.Single(x => x.Stage == "Interpretation").ReasonCodes);
        Assert.Equal(before, after.Items.Single().AvailableQuantity);
    }

    private sealed record LiveStatus(bool Enabled);
}
