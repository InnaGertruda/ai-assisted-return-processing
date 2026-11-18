using System.Text.Json.Serialization;

namespace ReturnFlow.Application;

public static class AiContractVersions
{
    public const string Schema = "1.0";
    public const string ReplayPrompt = "replay-return-intent-v1";
    public const string LivePrompt = "live-return-intent-v1";
    public const string Prompt = ReplayPrompt;
}

public sealed record InterpretReturnMessageRequest(
    string DemoSessionId,
    string SelectedOrderId,
    string SubmissionKey,
    string Message,
    string Mode,
    string ClientPartition = "unavailable");

public sealed class ReturnIntentProposalV1
{
    public required string SchemaVersion { get; init; }

    [JsonConverter(typeof(ReturnIntentJsonConverter))]
    public required ReturnIntent Intent { get; init; }

    public required IReadOnlyList<ProposedReturnItemV1> Items { get; init; }

    public required IReadOnlyList<string> Ambiguities { get; init; }

    public required bool RequiresClarification { get; init; }
}

public sealed class ProposedReturnItemV1
{
    public string? CandidateOrderItemId { get; init; }

    public string? ProductMention { get; init; }

    public int? RequestedQuantity { get; init; }

    [JsonConverter(typeof(ReturnReasonJsonConverter))]
    public required ReturnReason Reason { get; init; }

    public required IReadOnlyList<string> EvidenceQuotes { get; init; }
}

public enum ReturnIntent
{
    CreateReturn,
    CancelReturn,
    AskStatus,
    Other,
    Unknown,
}

public enum ReturnReason
{
    ChangedMind,
    Defective,
    Unspecified,
    Other,
}

public sealed class ReturnIntentJsonConverter()
    : JsonStringEnumConverter<ReturnIntent>(namingPolicy: null, allowIntegerValues: false);

public sealed class ReturnReasonJsonConverter()
    : JsonStringEnumConverter<ReturnReason>(namingPolicy: null, allowIntegerValues: false);

public sealed record ReturnInterpretationContext(
    string DemoSessionId,
    string ScenarioId,
    string OrderId,
    IReadOnlyList<DemoOrderItemSummary> Items);

public sealed record InterpreterRequest(
    string ScenarioId,
    string Message,
    ReturnInterpretationContext Context,
    string Mode = "Replay",
    string ClientPartition = "unavailable");

public sealed record InterpreterResult(
    bool Matched,
    string? RawOutput,
    string? FailureReasonCode,
    string PromptVersion = AiContractVersions.ReplayPrompt);

public interface IReturnIntentInterpreter
{
    Task<InterpreterResult> InterpretAsync(
        InterpreterRequest request,
        CancellationToken cancellationToken);
}

public interface ILiveUsageLimiter
{
    Task<LiveUsageDecision> TryAcquireAsync(
        string sessionId,
        string clientPartition,
        DateTimeOffset now,
        CancellationToken cancellationToken);
}

public sealed record LiveUsageDecision(bool Allowed, string? FailureReasonCode = null);

public interface IReturnInterpretationStore
{
    Task<ReturnInterpretationContext?> GetContextAsync(
        string sessionId,
        string orderId,
        DateTimeOffset now,
        CancellationToken cancellationToken);

    Task<IdempotencyCheckResult> CheckIdempotencyAsync(
        string sessionId,
        string submissionKey,
        string payloadHash,
        DateTimeOffset now,
        CancellationToken cancellationToken);

    Task<AiExecutionTrace> SaveTraceAsync(
        AiExecutionTrace trace,
        string submissionKey,
        string payloadHash,
        CancellationToken cancellationToken);

    Task<AiExecutionTrace> CommitAsync(
        ReturnCommitRequest request,
        CancellationToken cancellationToken);

    Task<AiExecutionTrace?> GetTraceAsync(
        string sessionId,
        string runId,
        DateTimeOffset now,
        CancellationToken cancellationToken);
}

public sealed record AiExecutionTrace(
    string RunId,
    string DemoSessionId,
    string ScenarioId,
    string Mode,
    DateTimeOffset ReceivedAt,
    string PromptVersion,
    string SchemaVersion,
    string FinalOutcome,
    string CommitStatus,
    string? CreatedReturnCaseId,
    ReturnIntentProposalV1? Proposal,
    IReadOnlyList<AiStageResult> Stages);

public enum IdempotencyCheckStatus
{
    NotFound,
    Match,
    Conflict,
}

public sealed record IdempotencyCheckResult(
    IdempotencyCheckStatus Status,
    AiExecutionTrace? Trace = null);

public sealed record ReturnCommitRequest(
    InterpretReturnMessageRequest Intake,
    string PayloadHash,
    ReturnIntentProposalV1 Proposal,
    AiExecutionTrace PreCommitTrace);

public sealed record AiStageResult(
    string Stage,
    string Status,
    IReadOnlyList<string> ReasonCodes,
    IReadOnlyList<string> RuleReferences,
    IReadOnlyList<SafeEvidence> SafeEvidence);

public sealed record SafeEvidence(string Kind, string Value);

public sealed class AiResourceNotFoundException : Exception;

public sealed class AiSubmissionConflictException : Exception
{
    public const string SafeReasonCode = "SUBMISSION_KEY_PAYLOAD_CONFLICT";
}
