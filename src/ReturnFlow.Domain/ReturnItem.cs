namespace ReturnFlow.Domain;

public sealed class ReturnItem
{
    private ReturnItem()
    {
    }

    public ReturnItem(
        string id,
        string returnCaseId,
        string orderId,
        string orderItemId,
        string demoSessionId,
        int quantity,
        ReturnItemState state)
    {
        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "Return quantity must be positive.");
        }

        Id = DomainText.Required(id, nameof(id));
        ReturnCaseId = DomainText.Required(returnCaseId, nameof(returnCaseId));
        OrderId = DomainText.Required(orderId, nameof(orderId));
        OrderItemId = DomainText.Required(orderItemId, nameof(orderItemId));
        DemoSessionId = DomainText.Required(demoSessionId, nameof(demoSessionId));
        Quantity = quantity;
        State = state;
    }

    public string Id { get; private set; } = string.Empty;

    public string ReturnCaseId { get; private set; } = string.Empty;

    public string OrderId { get; private set; } = string.Empty;

    public string OrderItemId { get; private set; } = string.Empty;

    public string DemoSessionId { get; private set; } = string.Empty;

    public int Quantity { get; private set; }

    public ReturnItemState State { get; private set; }
}

public enum ReturnItemState
{
    Reserved = 0,
    Released = 1,
    CompletedReturn = 2,
}
