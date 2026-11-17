namespace ReturnFlow.Domain;

public static class ReturnCapacity
{
    public static ReturnCapacitySummary Calculate(
        int purchasedQuantity,
        IEnumerable<(ReturnItemState State, int Quantity)> returnItems)
    {
        if (purchasedQuantity <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(purchasedQuantity),
                "Purchased quantity must be positive.");
        }

        ArgumentNullException.ThrowIfNull(returnItems);

        var activeAllocated = 0;
        var completedConsumed = 0;

        foreach (var (state, quantity) in returnItems)
        {
            if (quantity <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(returnItems),
                    "Every return-item quantity must be positive.");
            }

            switch (state)
            {
                case ReturnItemState.Reserved:
                    activeAllocated += quantity;
                    break;
                case ReturnItemState.CompletedReturn:
                    completedConsumed += quantity;
                    break;
                case ReturnItemState.Released:
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(returnItems), "Unknown return-item state.");
            }
        }

        return new ReturnCapacitySummary(
            purchasedQuantity,
            activeAllocated,
            completedConsumed,
            purchasedQuantity - activeAllocated - completedConsumed);
    }
}

public sealed record ReturnCapacitySummary(
    int PurchasedQuantity,
    int ActiveAllocatedQuantity,
    int CompletedConsumedQuantity,
    int AvailableQuantity);
