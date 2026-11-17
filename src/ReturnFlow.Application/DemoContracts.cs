namespace ReturnFlow.Application;

public sealed record DemoScenarioSummary(
    string Id,
    string Name,
    string Description,
    string FixtureVersion);

public sealed record DemoSessionSnapshot(
    string SessionId,
    string ScenarioId,
    DateTimeOffset ExpiresAt,
    DemoOrderSummary Order);

public sealed record DemoOrderSummary(
    string Id,
    string DisplayNumber,
    IReadOnlyList<DemoOrderItemSummary> Items);

public sealed record DemoOrderItemSummary(
    string Id,
    string ProductCode,
    string ProductName,
    int PurchasedQuantity,
    int ActiveAllocatedQuantity,
    int CompletedConsumedQuantity,
    int AvailableQuantity,
    IReadOnlyList<string> RelatedReturnCaseIds);

public sealed record DemoReturnCaseSummary(
    string Id,
    string OrderId,
    string Origin,
    DateTimeOffset CreatedAt,
    IReadOnlyList<DemoReturnItemSummary> Items);

public sealed record DemoReturnItemSummary(
    string Id,
    string OrderItemId,
    int Quantity,
    string State);
