namespace ReturnFlow.Domain;

public sealed class ReturnSubmission
{
    private ReturnSubmission()
    {
    }

    public ReturnSubmission(
        string id,
        string orderId,
        string demoSessionId,
        string submissionKey,
        string message,
        SubmissionChannel channel,
        DateTimeOffset receivedAt)
    {
        Id = DomainText.Required(id, nameof(id));
        OrderId = DomainText.Required(orderId, nameof(orderId));
        DemoSessionId = DomainText.Required(demoSessionId, nameof(demoSessionId));
        SubmissionKey = DomainText.Required(submissionKey, nameof(submissionKey));
        Message = DomainText.Required(message, nameof(message));
        Channel = channel;
        ReceivedAt = receivedAt;
    }

    public string Id { get; private set; } = string.Empty;

    public string OrderId { get; private set; } = string.Empty;

    public string DemoSessionId { get; private set; } = string.Empty;

    public string SubmissionKey { get; private set; } = string.Empty;

    public string Message { get; private set; } = string.Empty;

    public SubmissionChannel Channel { get; private set; }

    public DateTimeOffset ReceivedAt { get; private set; }
}

public enum SubmissionChannel
{
    SimulatedEmail = 0,
}
