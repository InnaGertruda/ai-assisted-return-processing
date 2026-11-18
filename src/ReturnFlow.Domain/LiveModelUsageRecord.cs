namespace ReturnFlow.Domain;

public sealed class LiveModelUsageRecord
{
    private LiveModelUsageRecord()
    {
    }

    public LiveModelUsageRecord(
        string id,
        string demoSessionId,
        string clientPartition,
        DateTimeOffset requestedAt)
    {
        Id = DomainText.Required(id, nameof(id));
        DemoSessionId = DomainText.Required(demoSessionId, nameof(demoSessionId));
        ClientPartition = DomainText.Required(clientPartition, nameof(clientPartition));
        RequestedAt = requestedAt;
    }

    public string Id { get; private set; } = string.Empty;

    public string DemoSessionId { get; private set; } = string.Empty;

    public string ClientPartition { get; private set; } = string.Empty;

    public DateTimeOffset RequestedAt { get; private set; }
}
