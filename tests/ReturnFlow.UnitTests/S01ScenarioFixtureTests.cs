using ReturnFlow.Infrastructure.DemoData;
using Xunit;

namespace ReturnFlow.UnitTests;

public sealed class S01ScenarioFixtureTests
{
    [Fact]
    public void Same_session_seed_is_deterministic()
    {
        var createdAt = new DateTimeOffset(2026, 11, 17, 12, 0, 0, TimeSpan.Zero);
        var expiresAt = createdAt.AddHours(2);

        var first = S01ScenarioFixture.Create("fixed-session", createdAt, expiresAt);
        var second = S01ScenarioFixture.Create("fixed-session", createdAt, expiresAt);

        Assert.Equal(first.Session.Id, second.Session.Id);
        Assert.Equal(first.Session.ScenarioId, second.Session.ScenarioId);
        Assert.Equal(first.Order.Id, second.Order.Id);
        Assert.Equal(first.OrderItem.Id, second.OrderItem.Id);
        Assert.Equal(first.Submission.Id, second.Submission.Id);
        Assert.Equal(first.ReturnCase.Id, second.ReturnCase.Id);
        Assert.Equal(first.ReturnItem.Id, second.ReturnItem.Id);
        Assert.Equal(3, first.OrderItem.PurchasedQuantity);
        Assert.Equal(1, first.ReturnItem.Quantity);
    }
}
