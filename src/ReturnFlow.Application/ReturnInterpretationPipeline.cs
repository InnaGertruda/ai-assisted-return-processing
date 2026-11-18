using System.Text.Json;
using System.Text.Json.Serialization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace ReturnFlow.Application;

public sealed class ReturnInterpretationPipeline(
    IReturnIntentInterpreter interpreter,
    IReturnInterpretationStore store,
    TimeProvider timeProvider)
{
    private const int MaximumMessageLength = 2_000;
    private const int MaximumItems = 8;
    private const int MaximumEvidenceQuotes = 8;

    private static readonly JsonSerializerOptions StrictJsonOptions = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    public async Task<AiExecutionTrace> RunAsync(
        InterpretReturnMessageRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var now = timeProvider.GetUtcNow();
        var runId = $"airun-{Guid.NewGuid():N}";
        var stages = new List<AiStageResult>();
        var payloadHash = CreatePayloadHash(request);

        if (string.IsNullOrWhiteSpace(request.DemoSessionId)
            || string.IsNullOrWhiteSpace(request.SelectedOrderId))
        {
            throw new AiResourceNotFoundException();
        }

        var inputFailure = ValidateInput(request);
        if (inputFailure is not null)
        {
            var inputContext = await store.GetContextAsync(
                request.DemoSessionId.Trim(),
                request.SelectedOrderId.Trim(),
                now,
                cancellationToken);
            if (inputContext is null)
            {
                throw new AiResourceNotFoundException();
            }

            stages.Add(Failed("InputGuardrails", inputFailure, "BR-RET-010"));
            AddSkipped(stages, "ContextRetrieval", "Interpretation", "SchemaVerification",
                "GroundingVerification", "BusinessValidation", "Commit");
            return await SaveAsync(CreateTrace(
                runId, request, inputContext.ScenarioId, now, "CannotProceed", null, stages),
                request, payloadHash, cancellationToken);
        }

        stages.Add(Passed("InputGuardrails", "INPUT_ACCEPTED", "BR-RET-010"));
        var context = await store.GetContextAsync(
            request.DemoSessionId.Trim(),
            request.SelectedOrderId.Trim(),
            now,
            cancellationToken);
        if (context is null)
        {
            throw new AiResourceNotFoundException();
        }

        stages.Add(Passed(
            "ContextRetrieval",
            "SESSION_ORDER_CONTEXT_SCOPED",
            "BR-RET-002",
            new SafeEvidence("scenarioId", context.ScenarioId)));

        var idempotency = await store.CheckIdempotencyAsync(
            request.DemoSessionId.Trim(),
            request.SubmissionKey.Trim(),
            payloadHash,
            now,
            cancellationToken);
        if (idempotency.Status == IdempotencyCheckStatus.Conflict)
        {
            throw new AiSubmissionConflictException();
        }

        if (idempotency.Status == IdempotencyCheckStatus.Match)
        {
            return idempotency.Trace!;
        }

        var interpreted = await interpreter.InterpretAsync(
            new InterpreterRequest(
                context.ScenarioId,
                request.Message.Trim(),
                context,
                NormalizeMode(request.Mode),
                request.ClientPartition),
            cancellationToken);
        if (!interpreted.Matched || interpreted.RawOutput is null)
        {
            stages.Add(Failed(
                "Interpretation",
                interpreted.FailureReasonCode ?? "REPLAY_FIXTURE_NOT_MATCHED",
                "BR-RET-010"));
            AddSkipped(stages, "SchemaVerification", "GroundingVerification", "BusinessValidation", "Commit");
            return await SaveAsync(CreateTrace(
                runId, request, context.ScenarioId, now, "TechnicalFailure", null, stages,
                interpreted.PromptVersion),
                request, payloadHash, cancellationToken);
        }

        stages.Add(Passed(
            "Interpretation",
            string.Equals(request.Mode, "Live", StringComparison.OrdinalIgnoreCase)
                ? "LIVE_PROVIDER_RESPONSE_RECEIVED"
                : "REPLAY_FIXTURE_MATCHED",
            "BR-RET-010",
            new SafeEvidence(
                string.Equals(request.Mode, "Live", StringComparison.OrdinalIgnoreCase)
                    ? "providerMode"
                    : "fixture",
                string.Equals(request.Mode, "Live", StringComparison.OrdinalIgnoreCase)
                    ? "Live"
                    : context.ScenarioId)));

        ReturnIntentProposalV1? proposal;
        try
        {
            proposal = JsonSerializer.Deserialize<ReturnIntentProposalV1>(
                interpreted.RawOutput,
                StrictJsonOptions);
            ValidateSchema(proposal);
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException)
        {
            stages.Add(Failed("SchemaVerification", "MODEL_OUTPUT_SCHEMA_INVALID", "BR-RET-010"));
            AddSkipped(stages, "GroundingVerification", "BusinessValidation", "Commit");
            return await SaveAsync(CreateTrace(
                runId, request, context.ScenarioId, now, "TechnicalFailure", null, stages,
                interpreted.PromptVersion),
                request, payloadHash, cancellationToken);
        }

        stages.Add(Passed("SchemaVerification", "MODEL_OUTPUT_SCHEMA_VALID", "BR-RET-003", "BR-RET-010"));

        var grounding = VerifyGrounding(request.Message, proposal!, context);
        stages.Add(grounding.Stage);
        if (grounding.Outcome is not null)
        {
            AddSkipped(stages, "BusinessValidation", "Commit");
            return await SaveAsync(CreateTrace(
                runId, request, context.ScenarioId, now, grounding.Outcome, proposal, stages,
                interpreted.PromptVersion),
                request, payloadHash, cancellationToken);
        }

        var business = ValidateBusiness(proposal!, context);
        stages.Add(business.Stage);
        if (business.Outcome is not null)
        {
            AddSkipped(stages, "Commit");
            return await SaveAsync(CreateTrace(
                runId, request, context.ScenarioId, now, business.Outcome, proposal, stages,
                interpreted.PromptVersion),
                request, payloadHash, cancellationToken);
        }

        var preCommitTrace = CreateTrace(
            runId, request, context.ScenarioId, now, "EligibleForCommit", proposal, stages,
            interpreted.PromptVersion);
        try
        {
            return await store.CommitAsync(
                new ReturnCommitRequest(request, payloadHash, proposal!, preCommitTrace),
                cancellationToken);
        }
        catch (Exception exception) when (exception is not (
            OperationCanceledException or AiResourceNotFoundException or AiSubmissionConflictException))
        {
            stages.Add(Failed(
                "Commit",
                "ATOMIC_COMMIT_TECHNICAL_FAILURE",
                "BR-RET-004",
                "BR-AUD-001"));
            var technicalFailure = CreateTrace(
                runId, request, context.ScenarioId, now, "TechnicalFailure", proposal, stages,
                interpreted.PromptVersion);
            return await SaveAsync(technicalFailure, request, payloadHash, cancellationToken);
        }
    }

    public async Task<AiExecutionTrace> GetAsync(
        string sessionId,
        string runId,
        CancellationToken cancellationToken = default) =>
        await store.GetTraceAsync(sessionId, runId, timeProvider.GetUtcNow(), cancellationToken)
        ?? throw new AiResourceNotFoundException();

    private async Task<AiExecutionTrace> SaveAsync(
        AiExecutionTrace trace,
        InterpretReturnMessageRequest request,
        string payloadHash,
        CancellationToken cancellationToken)
    {
        return await store.SaveTraceAsync(
            trace,
            request.SubmissionKey.Trim(),
            payloadHash,
            cancellationToken);
    }

    private static AiExecutionTrace CreateTrace(
        string runId,
        InterpretReturnMessageRequest request,
        string scenarioId,
        DateTimeOffset now,
        string outcome,
        ReturnIntentProposalV1? proposal,
        IReadOnlyList<AiStageResult> stages,
        string? promptVersion = null) => new(
            runId,
            request.DemoSessionId.Trim(),
            scenarioId,
            NormalizeMode(request.Mode),
            now,
            promptVersion ?? PromptVersionFor(request.Mode),
            AiContractVersions.Schema,
            outcome,
            "Not committed",
            null,
            proposal,
            stages);

    private static string CreatePayloadHash(InterpretReturnMessageRequest request)
    {
        var canonical = string.Join('\n',
            request.SelectedOrderId?.Trim() ?? string.Empty,
            request.Message?.Trim() ?? string.Empty,
            request.Mode?.Trim().ToUpperInvariant() ?? string.Empty);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)))
            .ToLowerInvariant();
    }

    private static string? ValidateInput(InterpretReturnMessageRequest request)
    {
        if (!string.Equals(request.Mode?.Trim(), "Replay", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(request.Mode?.Trim(), "Live", StringComparison.OrdinalIgnoreCase))
        {
            return "MODE_NOT_SUPPORTED";
        }

        if (string.IsNullOrWhiteSpace(request.SubmissionKey) || request.SubmissionKey.Trim().Length > 120)
        {
            return "SUBMISSION_KEY_INVALID";
        }

        if (string.IsNullOrWhiteSpace(request.Message) || request.Message.Length > MaximumMessageLength)
        {
            return "MESSAGE_INVALID";
        }

        return null;
    }

    private static string NormalizeMode(string? mode) =>
        string.Equals(mode?.Trim(), "Live", StringComparison.OrdinalIgnoreCase) ? "Live" : "Replay";

    private static string PromptVersionFor(string? mode) =>
        string.Equals(mode?.Trim(), "Live", StringComparison.OrdinalIgnoreCase)
            ? AiContractVersions.LivePrompt
            : AiContractVersions.ReplayPrompt;

    private static void ValidateSchema(ReturnIntentProposalV1? proposal)
    {
        if (proposal is null
            || proposal.SchemaVersion != AiContractVersions.Schema
            || proposal.Items is null
            || proposal.Items.Count > MaximumItems
            || proposal.Ambiguities is null
            || proposal.Ambiguities.Any(string.IsNullOrWhiteSpace))
        {
            throw new InvalidOperationException("Proposal contract is invalid.");
        }

        foreach (var item in proposal.Items)
        {
            if (item.EvidenceQuotes is null
                || item.EvidenceQuotes.Count > MaximumEvidenceQuotes
                || item.EvidenceQuotes.Any(string.IsNullOrWhiteSpace)
                || item.RequestedQuantity is <= 0)
            {
                throw new InvalidOperationException("Proposal item contract is invalid.");
            }
        }
    }

    private static VerificationResult VerifyGrounding(
        string originalMessage,
        ReturnIntentProposalV1 proposal,
        ReturnInterpretationContext context)
    {
        if (proposal.RequiresClarification || proposal.Ambiguities.Count > 0)
        {
            return new VerificationResult(
                Failed("GroundingVerification", "AMBIGUITY_UNRESOLVED", "BR-RET-008"),
                "ClarificationRequired");
        }

        if (proposal.Intent != ReturnIntent.CreateReturn)
        {
            return new VerificationResult(
                Passed("GroundingVerification", "INTENT_GROUNDED_UNSUPPORTED", "BR-RET-010"),
                null);
        }

        if (proposal.Items.Count == 0)
        {
            return new VerificationResult(
                Failed("GroundingVerification", "ITEM_REQUIRED", "BR-RET-008"),
                "ClarificationRequired");
        }

        foreach (var item in proposal.Items)
        {
            if (item.CandidateOrderItemId is null
                || context.Items.All(x => x.Id != item.CandidateOrderItemId))
            {
                return new VerificationResult(
                    Failed("GroundingVerification", "ITEM_ID_NOT_IN_CONTEXT", "BR-RET-002", "BR-RET-008"),
                    "ManualReview");
            }

            if (string.IsNullOrWhiteSpace(item.ProductMention) || item.RequestedQuantity is null)
            {
                return new VerificationResult(
                    Failed("GroundingVerification", "ITEM_OR_QUANTITY_UNSUPPORTED", "BR-RET-008"),
                    "ClarificationRequired");
            }

            var nameMatches = context.Items.Count(x =>
                x.ProductName.Contains(item.ProductMention, StringComparison.OrdinalIgnoreCase));
            if (nameMatches != 1)
            {
                return new VerificationResult(
                    Failed("GroundingVerification", "PRODUCT_MATCH_NOT_UNIQUE", "BR-RET-008"),
                    "ClarificationRequired");
            }

            if (item.EvidenceQuotes.Count == 0
                || item.EvidenceQuotes.Any(quote =>
                    !originalMessage.Contains(quote, StringComparison.OrdinalIgnoreCase)))
            {
                return new VerificationResult(
                    Failed("GroundingVerification", "EVIDENCE_QUOTE_NOT_PRESENT", "BR-RET-008"),
                    "ClarificationRequired");
            }

            if (!EvidenceSupportsQuantity(item.EvidenceQuotes, item.RequestedQuantity.Value))
            {
                return new VerificationResult(
                    Failed("GroundingVerification", "QUANTITY_EVIDENCE_CONFLICT", "BR-RET-003", "BR-RET-008"),
                    "ClarificationRequired");
            }
        }

        return new VerificationResult(
            Passed(
                "GroundingVerification",
                "ITEM_QUANTITY_EVIDENCE_GROUNDED",
                "BR-RET-002",
                "BR-RET-008"),
            null);
    }

    private static bool EvidenceSupportsQuantity(
        IReadOnlyList<string> evidenceQuotes,
        int requestedQuantity)
    {
        var wordValues = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["one"] = 1,
            ["two"] = 2,
            ["three"] = 3,
            ["four"] = 4,
            ["five"] = 5,
            ["six"] = 6,
            ["seven"] = 7,
            ["eight"] = 8,
        };
        var quantities = evidenceQuotes
            .SelectMany(quote => Regex.Matches(
                quote,
                @"\b(?:one|two|three|four|five|six|seven|eight|\d+)\b",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
            .Select(match => int.TryParse(match.Value, out var numeric)
                ? numeric
                : wordValues[match.Value])
            .Distinct()
            .ToArray();
        return quantities.Length == 1 && quantities[0] == requestedQuantity;
    }

    private static VerificationResult ValidateBusiness(
        ReturnIntentProposalV1 proposal,
        ReturnInterpretationContext context)
    {
        if (proposal.Intent != ReturnIntent.CreateReturn)
        {
            return new VerificationResult(
                Failed("BusinessValidation", "INTENT_NOT_SUPPORTED_IN_V1", "BR-RET-010"),
                "CannotProceed");
        }

        foreach (var proposed in proposal.Items)
        {
            var item = context.Items.Single(x => x.Id == proposed.CandidateOrderItemId);
            if (proposed.RequestedQuantity > item.AvailableQuantity)
            {
                return new VerificationResult(
                    Failed("BusinessValidation", "QUANTITY_EXCEEDS_AVAILABLE", "BR-RET-004"),
                    "CannotProceed");
            }
        }

        return new VerificationResult(
            Passed("BusinessValidation", "PREFLIGHT_ELIGIBLE", "BR-RET-003", "BR-RET-004"),
            null);
    }

    private static AiStageResult Passed(
        string stage,
        string reasonCode,
        string ruleReference,
        SafeEvidence? evidence = null) => new(
            stage,
            "Passed",
            [reasonCode],
            [ruleReference],
            evidence is null ? [] : [evidence]);

    private static AiStageResult Passed(
        string stage,
        string reasonCode,
        string firstRuleReference,
        string secondRuleReference) => new(
            stage,
            "Passed",
            [reasonCode],
            [firstRuleReference, secondRuleReference],
            []);

    private static AiStageResult Failed(
        string stage,
        string reasonCode,
        params string[] ruleReferences) => new(
            stage,
            "Failed",
            [reasonCode],
            ruleReferences,
            []);

    private static void AddSkipped(List<AiStageResult> stages, params string[] names)
    {
        stages.AddRange(names.Select(name => new AiStageResult(
            name,
            "Skipped",
            ["UPSTREAM_STAGE_DID_NOT_PASS"],
            [],
            [])));
    }

    private sealed record VerificationResult(AiStageResult Stage, string? Outcome);
}
