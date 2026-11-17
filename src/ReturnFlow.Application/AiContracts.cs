using System.Text.Json.Serialization;

namespace ReturnFlow.Application;

public static class AiContractVersions
{
    public const string Schema = "1.0";
    public const string Prompt = "replay-return-intent-v1";
}

public sealed record InterpretReturnMessageRequest(
    string DemoSessionId,
    string SelectedOrderId,
    string SubmissionKey,
    string Message,
    string Mode);

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
    ReturnInterpretationContext Context);

public sealed record InterpreterResult(bool Matched, string? RawOutput, string? FailureReasonCode);

public interface IReturnIntentInterpreter
{
    Task<InterpreterResult> InterpretAsync(
        InterpreterRequest request,
        CancellationToken cancellationToken);
}

public interface IReturnInterpretationStore
{
    Task<ReturnInterpretationContext?> GetContextAsync(
        string sessionId,
        string orderId,
        DateTimeOffset now,
        CancellationToken cancellationToken);

    Task SaveTraceAsync(AiExecutionTrace trace, CancellationToken cancellationToken);

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
    ReturnIntentProposalV1? Proposal,
    IReadOnlyList<AiStageResult> Stages);

public sealed record AiStageResult(
    string Stage,
    string Status,
    IReadOnlyList<string> ReasonCodes,
    IReadOnlyList<string> RuleReferences,
    IReadOnlyList<SafeEvidence> SafeEvidence);

public sealed record SafeEvidence(string Kind, string Value);

public sealed class AiResourceNotFoundException : Exception;
