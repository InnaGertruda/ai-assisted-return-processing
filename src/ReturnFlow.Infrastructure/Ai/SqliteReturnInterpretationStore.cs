using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ReturnFlow.Application;
using ReturnFlow.Domain;
using ReturnFlow.Infrastructure.Persistence;

namespace ReturnFlow.Infrastructure.Ai;

public sealed class SqliteReturnInterpretationStore(ReturnFlowDbContext dbContext)
    : IReturnInterpretationStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<ReturnInterpretationContext?> GetContextAsync(
        string sessionId,
        string orderId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var session = await dbContext.DemoSessions
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.Id == sessionId && x.ExpiresAt > now,
                cancellationToken);
        if (session is null)
        {
            return null;
        }

        var orderExists = await dbContext.Orders
            .AsNoTracking()
            .AnyAsync(
                x => x.Id == orderId && x.DemoSessionId == sessionId,
                cancellationToken);
        if (!orderExists)
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

        var summaries = orderItems.Select(item =>
        {
            var matching = returnItems.Where(x => x.OrderItemId == item.Id).ToArray();
            var capacity = ReturnCapacity.Calculate(
                item.PurchasedQuantity,
                matching.Select(x => (x.State, x.Quantity)));
            return new DemoOrderItemSummary(
                item.Id,
                item.ProductCode,
                item.ProductName,
                capacity.PurchasedQuantity,
                capacity.ActiveAllocatedQuantity,
                capacity.CompletedConsumedQuantity,
                capacity.AvailableQuantity,
                matching.Select(x => x.ReturnCaseId).Distinct().Order().ToArray());
        }).ToArray();

        return new ReturnInterpretationContext(sessionId, session.ScenarioId, orderId, summaries);
    }

    public async Task SaveTraceAsync(AiExecutionTrace trace, CancellationToken cancellationToken)
    {
        dbContext.AiTraces.Add(new AiTraceRecord(
            trace.RunId,
            trace.DemoSessionId,
            trace.ReceivedAt,
            JsonSerializer.Serialize(trace, JsonOptions)));
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<AiExecutionTrace?> GetTraceAsync(
        string sessionId,
        string runId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var payload = await dbContext.AiTraces
            .AsNoTracking()
            .Where(x => x.Id == runId
                && x.DemoSessionId == sessionId
                && dbContext.DemoSessions.Any(session =>
                    session.Id == sessionId && session.ExpiresAt > now))
            .Select(x => x.PayloadJson)
            .SingleOrDefaultAsync(cancellationToken);

        return payload is null
            ? null
            : JsonSerializer.Deserialize<AiExecutionTrace>(payload, JsonOptions);
    }
}
