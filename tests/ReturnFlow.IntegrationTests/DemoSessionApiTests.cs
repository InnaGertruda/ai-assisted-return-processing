using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ReturnFlow.Application;
using ReturnFlow.Domain;
using ReturnFlow.Infrastructure.Persistence;
using Xunit;

namespace ReturnFlow.IntegrationTests;

public sealed class DemoSessionApiTests : IClassFixture<ReturnFlowApiFactory>
{
    private readonly ReturnFlowApiFactory _factory;
    private readonly HttpClient _client;

    public DemoSessionApiTests(ReturnFlowApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task S01_sessions_are_isolated_and_start_with_expected_return_availability()
    {
        var first = await CreateS01SessionAsync(_client);
        var second = await CreateS01SessionAsync(_client);

        Assert.True(Guid.TryParseExact(first.SessionId, "N", out _));
        Assert.NotEqual(first.SessionId, second.SessionId);
        Assert.NotEqual(first.Order.Id, second.Order.Id);
        AssertS01Summary(first.Order);
        AssertS01Summary(second.Order);

        var crossSessionRead = await _client.GetAsync(
            $"/api/demo/sessions/{first.SessionId}/orders/{second.Order.Id}");
        Assert.Equal(HttpStatusCode.NotFound, crossSessionRead.StatusCode);
    }

    [Fact]
    public async Task Scenario_catalog_is_truthful_and_unknown_scenarios_are_rejected()
    {
        var scenarios = await _client.GetFromJsonAsync<DemoScenarioSummary[]>(
            "/api/demo/scenarios");
        var scenario = Assert.Single(scenarios!);
        Assert.Equal("S01", scenario.Id);
        Assert.Equal("1.0", scenario.FixtureVersion);

        var invalidResponse = await _client.PostAsJsonAsync(
            "/api/demo/sessions",
            new { scenarioId = "S99" });
        var error = await invalidResponse.Content.ReadFromJsonAsync<DemoError>();

        Assert.Equal(HttpStatusCode.BadRequest, invalidResponse.StatusCode);
        Assert.Equal("SCENARIO_NOT_FOUND", error!.ReasonCode);
    }

    [Fact]
    public async Task Reset_restores_only_the_requesting_session()
    {
        var first = await CreateS01SessionAsync(_client);
        var second = await CreateS01SessionAsync(_client);

        var resetResponse = await _client.PostAsync(
            $"/api/demo/sessions/{first.SessionId}/reset",
            content: null);
        resetResponse.EnsureSuccessStatusCode();
        var reset = await resetResponse.Content.ReadFromJsonAsync<DemoSessionSnapshot>();

        Assert.NotNull(reset);
        Assert.Equal(first.Order.Id, reset.Order.Id);
        AssertS01Summary(reset.Order);

        var untouchedResponse = await _client.GetAsync(
            $"/api/demo/sessions/{second.SessionId}/orders/{second.Order.Id}");
        untouchedResponse.EnsureSuccessStatusCode();
        var untouched = await untouchedResponse.Content.ReadFromJsonAsync<DemoOrderSummary>();
        Assert.NotNull(untouched);
        AssertS01Summary(untouched);

        var cases = await _client.GetFromJsonAsync<DemoReturnCaseSummary[]>(
            $"/api/demo/sessions/{second.SessionId}/return-cases");
        var seededCase = Assert.Single(cases!);
        Assert.Equal("SeedFixture", seededCase.Origin);

        var traceRead = await _client.GetAsync(
            $"/api/demo/sessions/{first.SessionId}/ai-runs/not-created-in-ai-1");
        Assert.Equal(HttpStatusCode.NotFound, traceRead.StatusCode);
    }

    [Fact]
    public async Task Expired_session_reads_and_reset_fail_closed()
    {
        var session = await CreateS01SessionAsync(_client);
        _factory.TimeProvider.Advance(TimeSpan.FromMinutes(121));

        var read = await _client.GetAsync(
            $"/api/demo/sessions/{session.SessionId}/orders/{session.Order.Id}");
        var reset = await _client.PostAsync(
            $"/api/demo/sessions/{session.SessionId}/reset",
            content: null);

        Assert.Equal(HttpStatusCode.NotFound, read.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, reset.StatusCode);
    }

    [Fact]
    public async Task Database_foreign_keys_reject_cross_session_order_item_membership()
    {
        var first = await CreateS01SessionAsync(_client);
        var second = await CreateS01SessionAsync(_client);

        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ReturnFlowDbContext>();
        var firstCase = await dbContext.ReturnCases
            .AsNoTracking()
            .SingleAsync(x => x.DemoSessionId == first.SessionId);
        var secondItem = Assert.Single(second.Order.Items);

        dbContext.ReturnItems.Add(new ReturnItem(
            "ri-invalid-cross-session",
            firstCase.Id,
            first.Order.Id,
            secondItem.Id,
            first.SessionId,
            1,
            ReturnItemState.Reserved));

        await Assert.ThrowsAsync<DbUpdateException>(() => dbContext.SaveChangesAsync());
    }

    [Fact]
    public async Task Separate_empty_databases_rebuild_the_same_fixture_for_a_fixed_session_id()
    {
        const string fixedSessionId = "fixed-session-id-for-rebuild-proof";
        using var firstFactory = new ReturnFlowApiFactory(fixedSessionId);
        using var secondFactory = new ReturnFlowApiFactory(fixedSessionId);

        var first = await CreateS01SessionAsync(firstFactory.CreateClient());
        var second = await CreateS01SessionAsync(secondFactory.CreateClient());

        Assert.Equivalent(first, second, strict: true);
        AssertS01Summary(first.Order);
    }

    private static async Task<DemoSessionSnapshot> CreateS01SessionAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync(
            "/api/demo/sessions",
            new { scenarioId = "S01" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<DemoSessionSnapshot>())!;
    }

    private static void AssertS01Summary(DemoOrderSummary order)
    {
        var lamp = Assert.Single(order.Items);
        Assert.Equal(3, lamp.PurchasedQuantity);
        Assert.Equal(1, lamp.ActiveAllocatedQuantity);
        Assert.Equal(0, lamp.CompletedConsumedQuantity);
        Assert.Equal(2, lamp.AvailableQuantity);
        Assert.Single(lamp.RelatedReturnCaseIds);
    }

    private sealed record DemoError(string ReasonCode);
}
