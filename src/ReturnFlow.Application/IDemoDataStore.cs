namespace ReturnFlow.Application;

public interface IDemoDataStore
{
    Task<IReadOnlyList<DemoScenarioSummary>> GetScenariosAsync(CancellationToken cancellationToken);

    Task DeleteExpiredSessionsAsync(DateTimeOffset now, CancellationToken cancellationToken);

    Task<DemoSessionSnapshot?> CreateSessionAsync(
        string sessionId,
        string scenarioId,
        DateTimeOffset createdAt,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken);

    Task<DemoOrderSummary?> GetOrderAsync(
        string sessionId,
        string orderId,
        DateTimeOffset now,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<DemoReturnCaseSummary>?> GetReturnCasesAsync(
        string sessionId,
        DateTimeOffset now,
        CancellationToken cancellationToken);

    Task<DemoSessionSnapshot?> ResetSessionAsync(
        string sessionId,
        DateTimeOffset resetAt,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken);
}
