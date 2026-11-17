namespace ReturnFlow.Domain;

public sealed class OrderItem
{
    private OrderItem()
    {
    }

    public OrderItem(
        string id,
        string orderId,
        string demoSessionId,
        string productCode,
        string productName,
        int purchasedQuantity)
    {
        if (purchasedQuantity <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(purchasedQuantity),
                "Purchased quantity must be positive.");
        }

        Id = DomainText.Required(id, nameof(id));
        OrderId = DomainText.Required(orderId, nameof(orderId));
        DemoSessionId = DomainText.Required(demoSessionId, nameof(demoSessionId));
        ProductCode = DomainText.Required(productCode, nameof(productCode));
        ProductName = DomainText.Required(productName, nameof(productName));
        PurchasedQuantity = purchasedQuantity;
    }

    public string Id { get; private set; } = string.Empty;

    public string OrderId { get; private set; } = string.Empty;

    public string DemoSessionId { get; private set; } = string.Empty;

    public string ProductCode { get; private set; } = string.Empty;

    public string ProductName { get; private set; } = string.Empty;

    public int PurchasedQuantity { get; private set; }
}
