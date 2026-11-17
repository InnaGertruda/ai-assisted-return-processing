using ReturnFlow.Domain;
using Xunit;

namespace ReturnFlow.UnitTests;

public sealed class ReturnCapacityTests
{
    [Fact]
    public void Released_quantity_is_available_while_completed_quantity_remains_consumed()
    {
        var result = ReturnCapacity.Calculate(
            4,
            [
                (ReturnItemState.Reserved, 1),
                (ReturnItemState.CompletedReturn, 1),
                (ReturnItemState.Released, 1),
            ]);

        Assert.Equal(4, result.PurchasedQuantity);
        Assert.Equal(1, result.ActiveAllocatedQuantity);
        Assert.Equal(1, result.CompletedConsumedQuantity);
        Assert.Equal(2, result.AvailableQuantity);
    }

    [Fact]
    public void Non_positive_return_item_quantity_is_rejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ReturnItem(
                "ri-test",
                "rc-test",
                "ord-test",
                "oi-test",
                "session-test",
                0,
                ReturnItemState.Reserved));
    }
}
