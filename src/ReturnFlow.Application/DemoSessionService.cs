namespace ReturnFlow.Application;

public sealed class DemoSessionService
{
    private readonly IDemoDataStore _dataStore;
    private readonly IDemoSessionIdGenerator _idGenerator;
    private readonly TimeProvider _timeProvider;
    private readonly TimeSpan _sessionLifetime;

    public DemoSessionService(
        IDemoDataStore dataStore,
        IDemoSessionIdGenerator idGenerator,
        TimeProvider timeProvider,
        TimeSpan sessionLifetime)
    {
        ArgumentNullException.ThrowIfNull(dataStore);
        ArgumentNullException.ThrowIfNull(idGenerator);
        ArgumentNullException.ThrowIfNull(timeProvider);

        if (sessionLifetime <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(sessionLifetime),
                "Session lifetime must be positive.");
        }

        _dataStore = dataStore;
        _idGenerator = idGenerator;
        _timeProvider = timeProvider;
        _sessionLifetime = sessionLifetime;
    }

    public Task<IReadOnlyList<DemoScenarioSummary>> GetScenariosAsync(
        CancellationToken cancellationToken = default) =>
        _dataStore.GetScenariosAsync(cancellationToken);

    public async Task<DemoSessionSnapshot> CreateSessionAsync(
        string scenarioId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(scenarioId))
        {
            throw new DemoRequestException("SCENARIO_ID_REQUIRED");
        }

        var now = _timeProvider.GetUtcNow();
        await _dataStore.DeleteExpiredSessionsAsync(now, cancellationToken);

        var session = await _dataStore.CreateSessionAsync(
            _idGenerator.Create(),
            scenarioId.Trim(),
            now,
            now.Add(_sessionLifetime),
            cancellationToken);

        return session ?? throw new DemoRequestException("SCENARIO_NOT_FOUND");
    }

    public async Task<DemoOrderSummary> GetOrderAsync(
        string sessionId,
        string orderId,
        CancellationToken cancellationToken = default)
    {
        var order = await _dataStore.GetOrderAsync(
            sessionId,
            orderId,
            _timeProvider.GetUtcNow(),
            cancellationToken);

        return order ?? throw new DemoResourceNotFoundException();
    }

    public async Task<IReadOnlyList<DemoReturnCaseSummary>> GetReturnCasesAsync(
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        var cases = await _dataStore.GetReturnCasesAsync(
            sessionId,
            _timeProvider.GetUtcNow(),
            cancellationToken);

        return cases ?? throw new DemoResourceNotFoundException();
    }

    public async Task<DemoSessionSnapshot> ResetSessionAsync(
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        var now = _timeProvider.GetUtcNow();
        var session = await _dataStore.ResetSessionAsync(
            sessionId,
            now,
            now.Add(_sessionLifetime),
            cancellationToken);

        return session ?? throw new DemoResourceNotFoundException();
    }
}

public sealed class DemoRequestException(string reasonCode) : Exception(reasonCode)
{
    public string ReasonCode { get; } = reasonCode;
}

public sealed class DemoResourceNotFoundException : Exception
{
    public const string SafeReasonCode = "DEMO_RESOURCE_NOT_FOUND";

    public DemoResourceNotFoundException()
        : base(SafeReasonCode)
    {
    }
}
