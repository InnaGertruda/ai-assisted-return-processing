namespace ReturnFlow.Domain;

public sealed class DemoSession
{
    private DemoSession()
    {
    }

    public DemoSession(
        string id,
        string scenarioId,
        DateTimeOffset createdAt,
        DateTimeOffset expiresAt)
    {
        Id = DomainText.Required(id, nameof(id));
        ScenarioId = DomainText.Required(scenarioId, nameof(scenarioId));

        if (expiresAt <= createdAt)
        {
            throw new ArgumentOutOfRangeException(nameof(expiresAt), "Session expiry must be after creation.");
        }

        CreatedAt = createdAt;
        ExpiresAt = expiresAt;
    }

    public string Id { get; private set; } = string.Empty;

    public string ScenarioId { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    public bool IsExpired(DateTimeOffset now) => now >= ExpiresAt;

    public void ResetLifetime(DateTimeOffset createdAt, DateTimeOffset expiresAt)
    {
        if (expiresAt <= createdAt)
        {
            throw new ArgumentOutOfRangeException(nameof(expiresAt), "Session expiry must be after creation.");
        }

        CreatedAt = createdAt;
        ExpiresAt = expiresAt;
    }
}
