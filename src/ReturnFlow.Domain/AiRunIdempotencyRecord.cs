namespace ReturnFlow.Domain;

public sealed class AiRunIdempotencyRecord
{
    private AiRunIdempotencyRecord()
    {
    }

    public AiRunIdempotencyRecord(
        string demoSessionId,
        string submissionKey,
        string payloadHash,
        string aiExecutionTraceId)
    {
        DemoSessionId = DomainText.Required(demoSessionId, nameof(demoSessionId));
        SubmissionKey = DomainText.Required(submissionKey, nameof(submissionKey));
        PayloadHash = DomainText.Required(payloadHash, nameof(payloadHash));
        AiExecutionTraceId = DomainText.Required(aiExecutionTraceId, nameof(aiExecutionTraceId));
    }

    public string DemoSessionId { get; private set; } = string.Empty;

    public string SubmissionKey { get; private set; } = string.Empty;

    public string PayloadHash { get; private set; } = string.Empty;

    public string AiExecutionTraceId { get; private set; } = string.Empty;
}
