namespace ReturnFlow.Domain;

public sealed class DemoOrder
{
    private DemoOrder()
    {
    }

    public DemoOrder(string id, string demoSessionId, string displayNumber)
    {
        Id = DomainText.Required(id, nameof(id));
        DemoSessionId = DomainText.Required(demoSessionId, nameof(demoSessionId));
        DisplayNumber = DomainText.Required(displayNumber, nameof(displayNumber));
    }

    public string Id { get; private set; } = string.Empty;

    public string DemoSessionId { get; private set; } = string.Empty;

    public string DisplayNumber { get; private set; } = string.Empty;
}
