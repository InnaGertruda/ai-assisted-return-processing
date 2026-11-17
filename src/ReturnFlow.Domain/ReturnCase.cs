namespace ReturnFlow.Domain;

public sealed class ReturnCase
{
    private ReturnCase()
    {
    }

    public ReturnCase(
        string id,
        string orderId,
        string demoSessionId,
        string returnSubmissionId,
        ReturnCaseOrigin origin,
        DateTimeOffset createdAt)
    {
        Id = DomainText.Required(id, nameof(id));
        OrderId = DomainText.Required(orderId, nameof(orderId));
        DemoSessionId = DomainText.Required(demoSessionId, nameof(demoSessionId));
        ReturnSubmissionId = DomainText.Required(returnSubmissionId, nameof(returnSubmissionId));
        Origin = origin;
        CreatedAt = createdAt;
    }

    public string Id { get; private set; } = string.Empty;

    public string OrderId { get; private set; } = string.Empty;

    public string DemoSessionId { get; private set; } = string.Empty;

    public string ReturnSubmissionId { get; private set; } = string.Empty;

    public ReturnCaseOrigin Origin { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
}

public enum ReturnCaseOrigin
{
    SeedFixture = 0,
    VerifiedSubmission = 1,
}
