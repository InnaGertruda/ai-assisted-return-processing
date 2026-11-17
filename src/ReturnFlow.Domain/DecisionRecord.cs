namespace ReturnFlow.Domain;

public sealed class DecisionRecord
{
    private DecisionRecord()
    {
    }

    public DecisionRecord(
        string id,
        string demoSessionId,
        string returnSubmissionId,
        string returnCaseId,
        string aiExecutionTraceId,
        string outcome,
        string ruleReferences,
        DateTimeOffset createdAt)
    {
        Id = DomainText.Required(id, nameof(id));
        DemoSessionId = DomainText.Required(demoSessionId, nameof(demoSessionId));
        ReturnSubmissionId = DomainText.Required(returnSubmissionId, nameof(returnSubmissionId));
        ReturnCaseId = DomainText.Required(returnCaseId, nameof(returnCaseId));
        AiExecutionTraceId = DomainText.Required(aiExecutionTraceId, nameof(aiExecutionTraceId));
        Outcome = DomainText.Required(outcome, nameof(outcome));
        RuleReferences = DomainText.Required(ruleReferences, nameof(ruleReferences));
        CreatedAt = createdAt;
    }

    public string Id { get; private set; } = string.Empty;
    public string DemoSessionId { get; private set; } = string.Empty;
    public string ReturnSubmissionId { get; private set; } = string.Empty;
    public string ReturnCaseId { get; private set; } = string.Empty;
    public string AiExecutionTraceId { get; private set; } = string.Empty;
    public string Outcome { get; private set; } = string.Empty;
    public string RuleReferences { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }
}
