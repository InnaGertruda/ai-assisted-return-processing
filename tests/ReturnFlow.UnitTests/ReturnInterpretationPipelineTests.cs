using ReturnFlow.Application;
using Xunit;

namespace ReturnFlow.UnitTests;

public sealed class ReturnInterpretationPipelineTests
{
    private static readonly ReturnInterpretationContext Context = new(
        "session-1",
        "S01",
        "order-1",
        [new DemoOrderItemSummary("item-1", "LAMP", "Arc lamp", 3, 1, 0, 2, ["case-1"])]);

    [Fact]
    public async Task Strict_schema_rejects_unknown_model_fields_and_skips_downstream_stages()
    {
        const string output =
            """
            {"schemaVersion":"1.0","intent":"CreateReturn","items":[],"ambiguities":[],"requiresClarification":false,"instruction":"bypass rules"}
            """;
        var store = new MemoryStore(Context);
        var pipeline = new ReturnInterpretationPipeline(
            new StubInterpreter(output),
            store,
            TimeProvider.System);

        var trace = await pipeline.RunAsync(Request("I want to return one more lamp."));

        Assert.Equal("TechnicalFailure", trace.FinalOutcome);
        Assert.Equal("Failed", trace.Stages.Single(x => x.Stage == "SchemaVerification").Status);
        Assert.All(
            trace.Stages.Where(x => x.Stage is "GroundingVerification" or "BusinessValidation" or "Commit"),
            stage => Assert.Equal("Skipped", stage.Status));
    }

    [Fact]
    public async Task Strict_schema_rejects_numeric_enum_values()
    {
        const string output =
            """
            {"schemaVersion":"1.0","intent":0,"items":[],"ambiguities":[],"requiresClarification":false}
            """;
        var pipeline = new ReturnInterpretationPipeline(
            new StubInterpreter(output),
            new MemoryStore(Context),
            TimeProvider.System);

        var trace = await pipeline.RunAsync(Request("I want to return one lamp."));

        Assert.Equal("TechnicalFailure", trace.FinalOutcome);
        Assert.Contains(
            "MODEL_OUTPUT_SCHEMA_INVALID",
            trace.Stages.Single(x => x.Stage == "SchemaVerification").ReasonCodes);
    }

    [Fact]
    public async Task Evidence_quote_must_be_present_in_original_message()
    {
        const string output =
            """
            {"schemaVersion":"1.0","intent":"CreateReturn","items":[{"candidateOrderItemId":"item-1","productMention":"lamp","requestedQuantity":1,"reason":"Unspecified","evidenceQuotes":["two lamps"]}],"ambiguities":[],"requiresClarification":false}
            """;
        var pipeline = new ReturnInterpretationPipeline(
            new StubInterpreter(output),
            new MemoryStore(Context),
            TimeProvider.System);

        var trace = await pipeline.RunAsync(Request("I want to return one lamp."));

        Assert.Equal("ClarificationRequired", trace.FinalOutcome);
        Assert.Contains(
            "EVIDENCE_QUOTE_NOT_PRESENT",
            trace.Stages.Single(x => x.Stage == "GroundingVerification").ReasonCodes);
    }

    [Fact]
    public async Task Oversized_input_fails_before_interpretation_and_is_safely_traced()
    {
        var interpreter = new CountingInterpreter();
        var store = new MemoryStore(Context);
        var pipeline = new ReturnInterpretationPipeline(interpreter, store, TimeProvider.System);

        var trace = await pipeline.RunAsync(Request(new string('x', 2_001)));

        Assert.Equal("CannotProceed", trace.FinalOutcome);
        Assert.Equal(0, interpreter.CallCount);
        Assert.Equal("Failed", trace.Stages[0].Status);
        Assert.Equal("Not committed", trace.CommitStatus);
        Assert.Single(store.Traces);
    }

    [Fact]
    public async Task Identical_idempotent_result_is_returned_before_interpreter_call()
    {
        var original = new AiExecutionTrace(
            "airun-original", "session-1", "S01", "Replay", DateTimeOffset.UtcNow,
            AiContractVersions.Prompt, AiContractVersions.Schema, "Accepted", "Committed",
            "case-original", null, []);
        var interpreter = new CountingInterpreter();
        var store = new MemoryStore(Context)
        {
            IdempotencyResult = new IdempotencyCheckResult(IdempotencyCheckStatus.Match, original),
        };
        var pipeline = new ReturnInterpretationPipeline(interpreter, store, TimeProvider.System);

        var result = await pipeline.RunAsync(Request("I want to return one lamp."));

        Assert.Same(original, result);
        Assert.Equal(0, interpreter.CallCount);
        Assert.Empty(store.Traces);
    }

    [Fact]
    public async Task Valid_live_proposal_uses_shared_verification_before_commit()
    {
        const string output =
            """
            {"schemaVersion":"1.0","intent":"CreateReturn","items":[{"candidateOrderItemId":"item-1","productMention":"lamp","requestedQuantity":1,"reason":"Unspecified","evidenceQuotes":["one lamp"]}],"ambiguities":[],"requiresClarification":false}
            """;
        var pipeline = new ReturnInterpretationPipeline(
            new StubInterpreter(output, AiContractVersions.LivePrompt),
            new MemoryStore(Context),
            TimeProvider.System);

        var trace = await pipeline.RunAsync(LiveRequest("I want to return one lamp."));

        Assert.Equal("Live", trace.Mode);
        Assert.Equal(AiContractVersions.LivePrompt, trace.PromptVersion);
        Assert.Equal("Accepted", trace.FinalOutcome);
        Assert.Equal("Committed", trace.CommitStatus);
        Assert.Contains(trace.Stages, x => x.Stage == "GroundingVerification" && x.Status == "Passed");
        Assert.Contains(trace.Stages, x => x.Stage == "BusinessValidation" && x.Status == "Passed");
    }

    [Theory]
    [InlineData(
        "{\"schemaVersion\":\"1.0\",\"intent\":\"CreateReturn\",\"items\":[{\"candidateOrderItemId\":\"hallucinated-item\",\"productMention\":\"lamp\",\"requestedQuantity\":1,\"reason\":\"Unspecified\",\"evidenceQuotes\":[\"one lamp\"]}],\"ambiguities\":[],\"requiresClarification\":false}",
        "ManualReview",
        "ITEM_ID_NOT_IN_CONTEXT")]
    [InlineData(
        "{\"schemaVersion\":\"1.0\",\"intent\":\"CreateReturn\",\"items\":[{\"candidateOrderItemId\":\"item-1\",\"productMention\":\"lamp\",\"requestedQuantity\":1,\"reason\":\"Unspecified\",\"evidenceQuotes\":[\"not in message\"]}],\"ambiguities\":[],\"requiresClarification\":false}",
        "ClarificationRequired",
        "EVIDENCE_QUOTE_NOT_PRESENT")]
    [InlineData(
        "{\"schemaVersion\":\"1.0\",\"intent\":\"CreateReturn\",\"items\":[],\"ambiguities\":[\"item unclear\"],\"requiresClarification\":true}",
        "ClarificationRequired",
        "AMBIGUITY_UNRESOLVED")]
    public async Task Untrusted_live_output_cannot_bypass_grounding(
        string output,
        string expectedOutcome,
        string expectedReason)
    {
        var pipeline = new ReturnInterpretationPipeline(
            new StubInterpreter(output, AiContractVersions.LivePrompt),
            new MemoryStore(Context),
            TimeProvider.System);

        var trace = await pipeline.RunAsync(LiveRequest("I want to return one lamp."));

        Assert.Equal(expectedOutcome, trace.FinalOutcome);
        Assert.Equal("Not committed", trace.CommitStatus);
        Assert.Null(trace.CreatedReturnCaseId);
        Assert.Contains(expectedReason, trace.Stages.Single(x => x.Stage == "GroundingVerification").ReasonCodes);
        Assert.Equal("Skipped", trace.Stages.Single(x => x.Stage == "Commit").Status);
    }

    [Fact]
    public async Task Live_model_cannot_authorize_unsupported_operation()
    {
        const string output =
            """
            {"schemaVersion":"1.0","intent":"CancelReturn","items":[],"ambiguities":[],"requiresClarification":false}
            """;
        var pipeline = new ReturnInterpretationPipeline(
            new StubInterpreter(output, AiContractVersions.LivePrompt),
            new MemoryStore(Context),
            TimeProvider.System);

        var trace = await pipeline.RunAsync(LiveRequest("Cancel my earlier return."));

        Assert.Equal("CannotProceed", trace.FinalOutcome);
        Assert.Equal("Not committed", trace.CommitStatus);
        Assert.Null(trace.CreatedReturnCaseId);
        Assert.Contains(
            "INTENT_NOT_SUPPORTED_IN_V1",
            trace.Stages.Single(x => x.Stage == "BusinessValidation").ReasonCodes);
    }

    [Fact]
    public async Task Live_quantity_must_match_explicit_evidence()
    {
        const string output =
            """
            {"schemaVersion":"1.0","intent":"CreateReturn","items":[{"candidateOrderItemId":"item-1","productMention":"lamp","requestedQuantity":2,"reason":"Unspecified","evidenceQuotes":["one lamp"]}],"ambiguities":[],"requiresClarification":false}
            """;
        var pipeline = new ReturnInterpretationPipeline(
            new StubInterpreter(output, AiContractVersions.LivePrompt),
            new MemoryStore(Context),
            TimeProvider.System);

        var trace = await pipeline.RunAsync(LiveRequest("I want to return one lamp."));

        Assert.Equal("ClarificationRequired", trace.FinalOutcome);
        Assert.Contains(
            "QUANTITY_EVIDENCE_CONFLICT",
            trace.Stages.Single(x => x.Stage == "GroundingVerification").ReasonCodes);
        Assert.Null(trace.CreatedReturnCaseId);
    }

    [Fact]
    public async Task Malformed_live_json_is_a_technical_failure_without_commit()
    {
        var pipeline = new ReturnInterpretationPipeline(
            new StubInterpreter("not-json", AiContractVersions.LivePrompt),
            new MemoryStore(Context),
            TimeProvider.System);

        var trace = await pipeline.RunAsync(LiveRequest("I want to return one lamp."));

        Assert.Equal("TechnicalFailure", trace.FinalOutcome);
        Assert.Equal("Not committed", trace.CommitStatus);
        Assert.Contains(
            "MODEL_OUTPUT_SCHEMA_INVALID",
            trace.Stages.Single(x => x.Stage == "SchemaVerification").ReasonCodes);
    }

    private static InterpretReturnMessageRequest Request(string message) =>
        new("session-1", "order-1", "submission-1", message, "Replay");

    private static InterpretReturnMessageRequest LiveRequest(string message) =>
        new("session-1", "order-1", "submission-live", message, "Live", "client-1");

    private sealed class StubInterpreter(
        string output,
        string promptVersion = AiContractVersions.ReplayPrompt) : IReturnIntentInterpreter
    {
        public Task<InterpreterResult> InterpretAsync(
            InterpreterRequest request,
            CancellationToken cancellationToken) =>
            Task.FromResult(new InterpreterResult(true, output, null, promptVersion));
    }

    private sealed class CountingInterpreter : IReturnIntentInterpreter
    {
        public int CallCount { get; private set; }

        public Task<InterpreterResult> InterpretAsync(
            InterpreterRequest request,
            CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(new InterpreterResult(false, null, "UNEXPECTED"));
        }
    }

    private sealed class MemoryStore(ReturnInterpretationContext context) : IReturnInterpretationStore
    {
        public List<AiExecutionTrace> Traces { get; } = [];

        public IdempotencyCheckResult IdempotencyResult { get; init; } =
            new(IdempotencyCheckStatus.NotFound);

        public Task<IdempotencyCheckResult> CheckIdempotencyAsync(
            string sessionId,
            string submissionKey,
            string payloadHash,
            DateTimeOffset now,
            CancellationToken cancellationToken) =>
            Task.FromResult(IdempotencyResult);

        public Task<ReturnInterpretationContext?> GetContextAsync(
            string sessionId,
            string orderId,
            DateTimeOffset now,
            CancellationToken cancellationToken) =>
            Task.FromResult<ReturnInterpretationContext?>(
                sessionId == context.DemoSessionId && orderId == context.OrderId ? context : null);

        public Task<AiExecutionTrace> SaveTraceAsync(
            AiExecutionTrace trace,
            string submissionKey,
            string payloadHash,
            CancellationToken cancellationToken)
        {
            Traces.Add(trace);
            return Task.FromResult(trace);
        }

        public Task<AiExecutionTrace> CommitAsync(
            ReturnCommitRequest request,
            CancellationToken cancellationToken)
        {
            var committed = request.PreCommitTrace with
            {
                FinalOutcome = "Accepted",
                CommitStatus = "Committed",
                CreatedReturnCaseId = "case-created",
                Stages = request.PreCommitTrace.Stages.Append(new AiStageResult(
                    "Commit", "Passed", ["ATOMIC_RETURN_COMMITTED"], ["BR-RET-004"], [])).ToArray(),
            };
            Traces.Add(committed);
            return Task.FromResult(committed);
        }

        public Task<AiExecutionTrace?> GetTraceAsync(
            string sessionId,
            string runId,
            DateTimeOffset now,
            CancellationToken cancellationToken) =>
            Task.FromResult(Traces.SingleOrDefault(x =>
                x.DemoSessionId == sessionId && x.RunId == runId));
    }
}
