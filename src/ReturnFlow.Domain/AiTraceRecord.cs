namespace ReturnFlow.Domain;

public sealed class AiTraceRecord
{
    private AiTraceRecord()
    {
    }

    public AiTraceRecord(
        string id,
        string demoSessionId,
        DateTimeOffset receivedAt,
        string payloadJson)
    {
        Id = DomainText.Required(id, nameof(id));
        DemoSessionId = DomainText.Required(demoSessionId, nameof(demoSessionId));
        ReceivedAt = receivedAt;
        PayloadJson = DomainText.Required(payloadJson, nameof(payloadJson));
    }

    public string Id { get; private set; } = string.Empty;

    public string DemoSessionId { get; private set; } = string.Empty;

    public DateTimeOffset ReceivedAt { get; private set; }

    public string PayloadJson { get; private set; } = string.Empty;
}
