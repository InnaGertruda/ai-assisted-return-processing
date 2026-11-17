using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ReturnFlow.Application;
using ReturnFlow.Infrastructure.Persistence;
using Xunit;

namespace ReturnFlow.IntegrationTests;

public sealed class AtomicReturnApiTests : IClassFixture<ReturnFlowApiFactory>
{
    private const string ClearMessage = "I want to return one more lamp.";
    private readonly ReturnFlowApiFactory _factory;
    private readonly HttpClient _client;

    public AtomicReturnApiTests(ReturnFlowApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task S01_commits_case_item_decision_trace_and_updates_authoritative_summary()
    {
        var session = await CreateSessionAsync("S01");
        var before = session.Order.Items.Single();

        var trace = await RunAsync(session, "s01-commit", ClearMessage);
        var after = await GetOrderAsync(session);

        Assert.Equal("Accepted", trace.FinalOutcome);
        Assert.Equal("Committed", trace.CommitStatus);
        Assert.NotNull(trace.CreatedReturnCaseId);
        Assert.Equal(before.ActiveAllocatedQuantity + 1, after.Items.Single().ActiveAllocatedQuantity);
        Assert.Equal(before.AvailableQuantity - 1, after.Items.Single().AvailableQuantity);

        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<ReturnFlowDbContext>();
        Assert.Equal(1, await database.DecisionRecords.CountAsync(x => x.DemoSessionId == session.SessionId));
    }

    [Fact]
    public async Task S03_capacity_preflight_creates_no_case()
    {
        var session = await CreateSessionAsync("S03");
        var beforeCases = await GetCasesAsync(session.SessionId);

        var trace = await RunAsync(session, "s03-over-capacity", "Return the remaining two lamps.");

        Assert.Equal("CannotProceed", trace.FinalOutcome);
        Assert.Equal("Not committed", trace.CommitStatus);
        Assert.Equal(beforeCases.Length, (await GetCasesAsync(session.SessionId)).Length);
    }

    [Fact]
    public async Task S04_identical_retry_returns_original_case_and_different_payload_conflicts()
    {
        var session = await CreateSessionAsync("S04");
        const string key = "s04-stable-key";

        var first = await RunAsync(session, key, ClearMessage);
        var retry = await RunAsync(session, key, ClearMessage);
        var conflict = await _client.PostAsJsonAsync(
            $"/api/demo/sessions/{session.SessionId}/ai-runs",
            RequestBody(session, key, "Different payload"));

        Assert.Equal(first.RunId, retry.RunId);
        Assert.Equal(first.CreatedReturnCaseId, retry.CreatedReturnCaseId);
        Assert.Equal(2, (await GetOrderAsync(session)).Items.Single().ActiveAllocatedQuantity);
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        Assert.Equal(2, (await GetCasesAsync(session.SessionId)).Length);
    }

    [Fact]
    public async Task S05_distinct_submission_keys_create_independent_cases_while_capacity_remains()
    {
        var session = await CreateSessionAsync("S05");

        var first = await RunAsync(session, "s05-first", ClearMessage);
        var second = await RunAsync(session, "s05-second", ClearMessage);

        Assert.Equal("Accepted", first.FinalOutcome);
        Assert.Equal("Accepted", second.FinalOutcome);
        Assert.NotEqual(first.CreatedReturnCaseId, second.CreatedReturnCaseId);
        Assert.Equal(0, (await GetOrderAsync(session)).Items.Single().AvailableQuantity);
    }

    [Fact]
    public async Task S10_two_connections_racing_for_final_unit_commit_exactly_one()
    {
        var session = await CreateSessionAsync("S10");
        var firstClient = _factory.CreateClient();
        var secondClient = _factory.CreateClient();

        var responses = await Task.WhenAll(
            RunAsync(firstClient, session, "s10-racer-a", ClearMessage),
            RunAsync(secondClient, session, "s10-racer-b", ClearMessage));

        Assert.Single(responses, x => x.FinalOutcome == "Accepted" && x.CommitStatus == "Committed");
        Assert.Single(responses, x => x.FinalOutcome == "CannotProceed" && x.CommitStatus == "Not committed");
        Assert.Equal(3, (await GetOrderAsync(session)).Items.Single().ActiveAllocatedQuantity);
        Assert.Equal(0, (await GetOrderAsync(session)).Items.Single().AvailableQuantity);
    }

    [Fact]
    public async Task Parallel_identical_requests_return_one_logical_result_and_one_allocation()
    {
        var session = await CreateSessionAsync("S04");
        var firstClient = _factory.CreateClient();
        var secondClient = _factory.CreateClient();

        var responses = await Task.WhenAll(
            RunAsync(firstClient, session, "parallel-same-key", ClearMessage),
            RunAsync(secondClient, session, "parallel-same-key", ClearMessage));

        Assert.Equal(responses[0].RunId, responses[1].RunId);
        Assert.Equal(responses[0].CreatedReturnCaseId, responses[1].CreatedReturnCaseId);
        Assert.Equal(2, (await GetOrderAsync(session)).Items.Single().ActiveAllocatedQuantity);
    }

    [Fact]
    public async Task Commit_failure_rolls_back_submission_case_items_decision_and_accepted_trace()
    {
        var session = await CreateSessionAsync("S01");
        await ExecuteSqlAsync(
            "CREATE TRIGGER FailDecisionRecord BEFORE INSERT ON DecisionRecords BEGIN SELECT RAISE(ABORT, 'injected failure'); END;");
        try
        {
            var trace = await RunAsync(session, "forced-rollback", ClearMessage);

            Assert.Equal("TechnicalFailure", trace.FinalOutcome);
            Assert.Equal("Not committed", trace.CommitStatus);
            Assert.Contains("ATOMIC_COMMIT_TECHNICAL_FAILURE",
                trace.Stages.Single(x => x.Stage == "Commit").ReasonCodes);

            await using var scope = _factory.Services.CreateAsyncScope();
            var database = scope.ServiceProvider.GetRequiredService<ReturnFlowDbContext>();
            Assert.Equal(1, await database.ReturnSubmissions.CountAsync(x => x.DemoSessionId == session.SessionId));
            Assert.Equal(1, await database.ReturnCases.CountAsync(x => x.DemoSessionId == session.SessionId));
            Assert.Equal(1, await database.ReturnItems.CountAsync(x => x.DemoSessionId == session.SessionId));
            Assert.Equal(0, await database.DecisionRecords.CountAsync(x => x.DemoSessionId == session.SessionId));
            Assert.Equal(0, await database.AiTraces.CountAsync(x =>
                x.DemoSessionId == session.SessionId && x.PayloadJson.Contains("\"finalOutcome\":\"Accepted\"")));
        }
        finally
        {
            await ExecuteSqlAsync("DROP TRIGGER IF EXISTS FailDecisionRecord;");
        }
    }

    [Fact]
    public async Task Multi_line_commit_is_all_or_nothing_when_one_line_has_insufficient_capacity()
    {
        var session = await CreateSessionAsync("S02");
        var lines = session.Order.Items.OrderBy(x => x.Id).ToArray();
        var intake = new InterpretReturnMessageRequest(
            session.SessionId, session.Order.Id, "multi-line-reject", "Direct verified multi-line test", "Replay");
        var proposal = new ReturnIntentProposalV1
        {
            SchemaVersion = AiContractVersions.Schema,
            Intent = ReturnIntent.CreateReturn,
            Ambiguities = [],
            RequiresClarification = false,
            Items =
            [
                new ProposedReturnItemV1
                {
                    CandidateOrderItemId = lines[0].Id,
                    ProductMention = lines[0].ProductName,
                    RequestedQuantity = 1,
                    Reason = ReturnReason.Unspecified,
                    EvidenceQuotes = ["Direct verified multi-line test"],
                },
                new ProposedReturnItemV1
                {
                    CandidateOrderItemId = lines[1].Id,
                    ProductMention = lines[1].ProductName,
                    RequestedQuantity = 2,
                    Reason = ReturnReason.Unspecified,
                    EvidenceQuotes = ["Direct verified multi-line test"],
                },
            ],
        };
        var preCommit = new AiExecutionTrace(
            $"airun-{Guid.NewGuid():N}", session.SessionId, "S02", "Replay",
            _factory.TimeProvider.GetUtcNow(), AiContractVersions.Prompt, AiContractVersions.Schema,
            "EligibleForCommit", "Not committed", null, proposal, []);

        await using var scope = _factory.Services.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IReturnInterpretationStore>();
        var result = await store.CommitAsync(new ReturnCommitRequest(
            intake, new string('a', 64), proposal, preCommit), CancellationToken.None);

        Assert.Equal("CannotProceed", result.FinalOutcome);
        Assert.Contains("AUTHORITATIVE_CAPACITY_UNAVAILABLE",
            result.Stages.Single(x => x.Stage == "Commit").ReasonCodes);
        Assert.Empty(await GetCasesAsync(session.SessionId));
        var order = await GetOrderAsync(session);
        Assert.All(order.Items, item => Assert.Equal(0, item.ActiveAllocatedQuantity));
    }

    private async Task<DemoSessionSnapshot> CreateSessionAsync(string scenarioId)
    {
        var response = await _client.PostAsJsonAsync("/api/demo/sessions", new { scenarioId });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<DemoSessionSnapshot>())!;
    }

    private Task<AiExecutionTrace> RunAsync(DemoSessionSnapshot session, string key, string message) =>
        RunAsync(_client, session, key, message);

    private static async Task<AiExecutionTrace> RunAsync(
        HttpClient client, DemoSessionSnapshot session, string key, string message)
    {
        var response = await client.PostAsJsonAsync(
            $"/api/demo/sessions/{session.SessionId}/ai-runs", RequestBody(session, key, message));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AiExecutionTrace>())!;
    }

    private static object RequestBody(DemoSessionSnapshot session, string key, string message) => new
    {
        selectedOrderId = session.Order.Id,
        submissionKey = key,
        message,
        mode = "Replay",
    };

    private async Task<DemoOrderSummary> GetOrderAsync(DemoSessionSnapshot session) =>
        (await _client.GetFromJsonAsync<DemoOrderSummary>(
            $"/api/demo/sessions/{session.SessionId}/orders/{session.Order.Id}"))!;

    private async Task<DemoReturnCaseSummary[]> GetCasesAsync(string sessionId) =>
        (await _client.GetFromJsonAsync<DemoReturnCaseSummary[]>(
            $"/api/demo/sessions/{sessionId}/return-cases"))!;

    private async Task ExecuteSqlAsync(string sql)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<ReturnFlowDbContext>();
        await database.Database.ExecuteSqlRawAsync(sql);
    }
}
