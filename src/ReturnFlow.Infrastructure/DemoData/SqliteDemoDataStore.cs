using Microsoft.EntityFrameworkCore;
using ReturnFlow.Application;
using ReturnFlow.Domain;
using ReturnFlow.Infrastructure.Persistence;

namespace ReturnFlow.Infrastructure.DemoData;

public sealed class SqliteDemoDataStore(ReturnFlowDbContext dbContext) : IDemoDataStore
{
    public Task<IReadOnlyList<DemoScenarioSummary>> GetScenariosAsync(
        CancellationToken cancellationToken)
    {
        return Task.FromResult(ReplayScenarioFixtures.Catalog);
    }

    public async Task DeleteExpiredSessionsAsync(
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var expiredSessionIds = await dbContext.DemoSessions
            .Where(x => x.ExpiresAt <= now)
            .Select(x => x.Id)
            .ToArrayAsync(cancellationToken);

        foreach (var sessionId in expiredSessionIds)
        {
            await DeleteSessionAsync(sessionId, deleteSession: true, cancellationToken);
        }
    }

    public async Task<DemoSessionSnapshot?> CreateSessionAsync(
        string sessionId,
        string scenarioId,
        DateTimeOffset createdAt,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken)
    {
        var fixture = ReplayScenarioFixtures.Create(sessionId, scenarioId, createdAt, expiresAt);
        if (fixture is null)
        {
            return null;
        }

        dbContext.AddRange(
            fixture.Session,
            fixture.Order);
        dbContext.AddRange(fixture.OrderItems);
        dbContext.AddRange(fixture.Submissions);
        dbContext.AddRange(fixture.ReturnCases);
        dbContext.AddRange(fixture.ReturnItems);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new DemoSessionSnapshot(
            sessionId,
            scenarioId,
            expiresAt,
            (await QueryOrderAsync(sessionId, fixture.Order.Id, cancellationToken))!);
    }

    public async Task<DemoOrderSummary?> GetOrderAsync(
        string sessionId,
        string orderId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (!await HasActiveSessionAsync(sessionId, now, cancellationToken))
        {
            return null;
        }

        return await QueryOrderAsync(sessionId, orderId, cancellationToken);
    }

    public async Task<IReadOnlyList<DemoReturnCaseSummary>?> GetReturnCasesAsync(
        string sessionId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (!await HasActiveSessionAsync(sessionId, now, cancellationToken))
        {
            return null;
        }

        var cases = await dbContext.ReturnCases
            .AsNoTracking()
            .Where(x => x.DemoSessionId == sessionId)
            .OrderBy(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

        var caseIds = cases.Select(x => x.Id).ToArray();
        var items = await dbContext.ReturnItems
            .AsNoTracking()
            .Where(x => x.DemoSessionId == sessionId && caseIds.Contains(x.ReturnCaseId))
            .OrderBy(x => x.Id)
            .ToListAsync(cancellationToken);

        return cases
            .Select(returnCase => new DemoReturnCaseSummary(
                returnCase.Id,
                returnCase.OrderId,
                returnCase.Origin.ToString(),
                returnCase.CreatedAt,
                items.Where(x => x.ReturnCaseId == returnCase.Id)
                    .Select(x => new DemoReturnItemSummary(
                        x.Id,
                        x.OrderItemId,
                        x.Quantity,
                        x.State.ToString()))
                    .ToArray()))
            .ToArray();
    }

    public async Task<DemoSessionSnapshot?> ResetSessionAsync(
        string sessionId,
        DateTimeOffset resetAt,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken)
    {
        var session = await dbContext.DemoSessions
            .SingleOrDefaultAsync(x => x.Id == sessionId, cancellationToken);

        if (session is null)
        {
            return null;
        }

        if (session.IsExpired(resetAt))
        {
            await DeleteSessionAsync(sessionId, deleteSession: true, cancellationToken);
            return null;
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await DeleteSessionAsync(sessionId, deleteSession: false, cancellationToken);

        session.ResetLifetime(resetAt, expiresAt);
        dbContext.Update(session);
        var fixture = ReplayScenarioFixtures.Create(
            sessionId,
            session.ScenarioId,
            resetAt,
            expiresAt)!;
        dbContext.AddRange(
            fixture.Order);
        dbContext.AddRange(fixture.OrderItems);
        dbContext.AddRange(fixture.Submissions);
        dbContext.AddRange(fixture.ReturnCases);
        dbContext.AddRange(fixture.ReturnItems);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new DemoSessionSnapshot(
            sessionId,
            session.ScenarioId,
            expiresAt,
            (await QueryOrderAsync(sessionId, fixture.Order.Id, cancellationToken))!);
    }

    private async Task<bool> HasActiveSessionAsync(
        string sessionId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var session = await dbContext.DemoSessions
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == sessionId, cancellationToken);

        if (session is null)
        {
            return false;
        }

        if (!session.IsExpired(now))
        {
            return true;
        }

        await DeleteSessionAsync(sessionId, deleteSession: true, cancellationToken);
        return false;
    }

    private async Task<DemoOrderSummary?> QueryOrderAsync(
        string sessionId,
        string orderId,
        CancellationToken cancellationToken)
    {
        var order = await dbContext.Orders
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.Id == orderId && x.DemoSessionId == sessionId,
                cancellationToken);

        if (order is null)
        {
            return null;
        }

        var orderItems = await dbContext.OrderItems
            .AsNoTracking()
            .Where(x => x.OrderId == orderId && x.DemoSessionId == sessionId)
            .OrderBy(x => x.Id)
            .ToListAsync(cancellationToken);

        var returnItems = await dbContext.ReturnItems
            .AsNoTracking()
            .Where(x => x.OrderId == orderId && x.DemoSessionId == sessionId)
            .ToListAsync(cancellationToken);

        var itemSummaries = orderItems.Select(item =>
        {
            var matchingReturnItems = returnItems.Where(x => x.OrderItemId == item.Id).ToArray();
            var capacity = ReturnCapacity.Calculate(
                item.PurchasedQuantity,
                matchingReturnItems.Select(x => (x.State, x.Quantity)));

            return new DemoOrderItemSummary(
                item.Id,
                item.ProductCode,
                item.ProductName,
                capacity.PurchasedQuantity,
                capacity.ActiveAllocatedQuantity,
                capacity.CompletedConsumedQuantity,
                capacity.AvailableQuantity,
                matchingReturnItems.Select(x => x.ReturnCaseId).Distinct().Order().ToArray());
        }).ToArray();

        return new DemoOrderSummary(order.Id, order.DisplayNumber, itemSummaries);
    }

    private async Task DeleteSessionAsync(
        string sessionId,
        bool deleteSession,
        CancellationToken cancellationToken)
    {
        await dbContext.DecisionRecords
            .Where(x => x.DemoSessionId == sessionId)
            .ExecuteDeleteAsync(cancellationToken);
        await dbContext.AiRunIdempotencyRecords
            .Where(x => x.DemoSessionId == sessionId)
            .ExecuteDeleteAsync(cancellationToken);
        await dbContext.AiTraces
            .Where(x => x.DemoSessionId == sessionId)
            .ExecuteDeleteAsync(cancellationToken);
        await dbContext.ReturnItems
            .Where(x => x.DemoSessionId == sessionId)
            .ExecuteDeleteAsync(cancellationToken);
        await dbContext.ReturnCases
            .Where(x => x.DemoSessionId == sessionId)
            .ExecuteDeleteAsync(cancellationToken);
        await dbContext.ReturnSubmissions
            .Where(x => x.DemoSessionId == sessionId)
            .ExecuteDeleteAsync(cancellationToken);
        await dbContext.OrderItems
            .Where(x => x.DemoSessionId == sessionId)
            .ExecuteDeleteAsync(cancellationToken);
        await dbContext.Orders
            .Where(x => x.DemoSessionId == sessionId)
            .ExecuteDeleteAsync(cancellationToken);

        if (deleteSession)
        {
            await dbContext.DemoSessions
                .Where(x => x.Id == sessionId)
                .ExecuteDeleteAsync(cancellationToken);
        }

        dbContext.ChangeTracker.Clear();
    }
}
