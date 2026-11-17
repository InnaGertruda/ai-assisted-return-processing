using System.Data;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ReturnFlow.Application;
using ReturnFlow.Domain;
using ReturnFlow.Infrastructure.Persistence;

namespace ReturnFlow.Infrastructure.Ai;

public sealed class SqliteReturnInterpretationStore(ReturnFlowDbContext dbContext)
    : IReturnInterpretationStore
{
    private const int MaximumBusyAttempts = 4;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<ReturnInterpretationContext?> GetContextAsync(string sessionId, string orderId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var session = await dbContext.DemoSessions.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == sessionId && x.ExpiresAt > now, cancellationToken);
        if (session is null || !await dbContext.Orders.AsNoTracking().AnyAsync(
                x => x.Id == orderId && x.DemoSessionId == sessionId, cancellationToken))
        {
            return null;
        }

        var orderItems = await dbContext.OrderItems.AsNoTracking()
            .Where(x => x.OrderId == orderId && x.DemoSessionId == sessionId)
            .OrderBy(x => x.Id).ToListAsync(cancellationToken);
        var returnItems = await dbContext.ReturnItems.AsNoTracking()
            .Where(x => x.OrderId == orderId && x.DemoSessionId == sessionId)
            .ToListAsync(cancellationToken);
        var summaries = orderItems.Select(item =>
        {
            var matching = returnItems.Where(x => x.OrderItemId == item.Id).ToArray();
            var capacity = ReturnCapacity.Calculate(item.PurchasedQuantity, matching.Select(x => (x.State, x.Quantity)));
            return new DemoOrderItemSummary(item.Id, item.ProductCode, item.ProductName,
                capacity.PurchasedQuantity, capacity.ActiveAllocatedQuantity,
                capacity.CompletedConsumedQuantity, capacity.AvailableQuantity,
                matching.Select(x => x.ReturnCaseId).Distinct().Order().ToArray());
        }).ToArray();
        return new ReturnInterpretationContext(sessionId, session.ScenarioId, orderId, summaries);
    }

    public async Task<IdempotencyCheckResult> CheckIdempotencyAsync(
        string sessionId, string submissionKey, string payloadHash, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var existing = await FindIdempotencyAsync(sessionId, submissionKey, now, cancellationToken);
        return existing is null
            ? new IdempotencyCheckResult(IdempotencyCheckStatus.NotFound)
            : existing.Value.PayloadHash == payloadHash
                ? new IdempotencyCheckResult(IdempotencyCheckStatus.Match, existing.Value.Trace)
                : new IdempotencyCheckResult(IdempotencyCheckStatus.Conflict);
    }

    public Task<AiExecutionTrace> SaveTraceAsync(
        AiExecutionTrace trace, string submissionKey, string payloadHash, CancellationToken cancellationToken) =>
        ExecuteWriteAsync(async transaction =>
        {
            var existing = await FindIdempotencyInTransactionAsync(trace.DemoSessionId, submissionKey, cancellationToken);
            if (existing is not null)
            {
                if (existing.Value.PayloadHash != payloadHash) throw new AiSubmissionConflictException();
                await transaction.RollbackAsync(cancellationToken);
                return existing.Value.Trace;
            }

            AddTraceAndIdempotency(trace, submissionKey, payloadHash);
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return trace;
        }, cancellationToken);

    public Task<AiExecutionTrace> CommitAsync(ReturnCommitRequest request, CancellationToken cancellationToken) =>
        ExecuteWriteAsync(async transaction =>
        {
            var intake = request.Intake;
            var sessionId = intake.DemoSessionId.Trim();
            var submissionKey = intake.SubmissionKey.Trim();
            var existing = await FindIdempotencyInTransactionAsync(sessionId, submissionKey, cancellationToken);
            if (existing is not null)
            {
                if (existing.Value.PayloadHash != request.PayloadHash) throw new AiSubmissionConflictException();
                await transaction.RollbackAsync(cancellationToken);
                return existing.Value.Trace;
            }

            var now = request.PreCommitTrace.ReceivedAt;
            var orderId = intake.SelectedOrderId.Trim();
            var sessionExists = await dbContext.DemoSessions.AsNoTracking().AnyAsync(
                x => x.Id == sessionId && x.ExpiresAt > now, cancellationToken);
            var orderExists = await dbContext.Orders.AsNoTracking().AnyAsync(
                x => x.Id == orderId && x.DemoSessionId == sessionId, cancellationToken);
            if (!sessionExists || !orderExists) throw new AiResourceNotFoundException();

            var requested = request.Proposal.Items
                .GroupBy(x => x.CandidateOrderItemId!, StringComparer.Ordinal)
                .Select(x => new RequestedLine(x.Key, x.Sum(item => item.RequestedQuantity!.Value)))
                .OrderBy(x => x.OrderItemId, StringComparer.Ordinal).ToArray();
            if (requested.Length == 0 || requested.Any(x => x.Quantity <= 0))
            {
                return await SaveBusinessRejectionAsync(request, submissionKey,
                    "AUTHORITATIVE_QUANTITY_INVALID", transaction, cancellationToken);
            }

            var requestedIds = requested.Select(x => x.OrderItemId).ToArray();
            var orderItems = await dbContext.OrderItems.AsNoTracking()
                .Where(x => x.OrderId == orderId && x.DemoSessionId == sessionId && requestedIds.Contains(x.Id))
                .OrderBy(x => x.Id).ToListAsync(cancellationToken);
            if (orderItems.Count != requested.Length)
            {
                return await SaveBusinessRejectionAsync(request, submissionKey,
                    "AUTHORITATIVE_ITEM_SCOPE_INVALID", transaction, cancellationToken);
            }

            var allocated = await dbContext.ReturnItems.AsNoTracking()
                .Where(x => x.OrderId == orderId && x.DemoSessionId == sessionId && requestedIds.Contains(x.OrderItemId))
                .ToListAsync(cancellationToken);
            foreach (var line in requested)
            {
                var orderItem = orderItems.Single(x => x.Id == line.OrderItemId);
                var capacity = ReturnCapacity.Calculate(orderItem.PurchasedQuantity,
                    allocated.Where(x => x.OrderItemId == line.OrderItemId).Select(x => (x.State, x.Quantity)));
                if (line.Quantity > capacity.AvailableQuantity)
                {
                    return await SaveBusinessRejectionAsync(request, submissionKey,
                        "AUTHORITATIVE_CAPACITY_UNAVAILABLE", transaction, cancellationToken);
                }
            }

            var suffix = Guid.NewGuid().ToString("N");
            var submissionId = $"sub-{suffix}";
            var returnCaseId = $"rc-{suffix}";
            var accepted = request.PreCommitTrace with
            {
                FinalOutcome = "Accepted",
                CommitStatus = "Committed",
                CreatedReturnCaseId = returnCaseId,
                Stages = request.PreCommitTrace.Stages.Append(new AiStageResult(
                    "Commit", "Passed", ["ATOMIC_RETURN_COMMITTED"],
                    ["BR-RET-001", "BR-RET-002", "BR-RET-003", "BR-RET-004", "BR-RET-006"],
                    [new SafeEvidence("returnCaseId", returnCaseId)])).ToArray(),
            };

            dbContext.ReturnSubmissions.Add(new ReturnSubmission(submissionId, orderId, sessionId,
                submissionKey, intake.Message.Trim(), SubmissionChannel.SimulatedEmail, now));
            dbContext.ReturnCases.Add(new ReturnCase(returnCaseId, orderId, sessionId,
                submissionId, ReturnCaseOrigin.VerifiedSubmission, now));
            foreach (var line in requested)
            {
                dbContext.ReturnItems.Add(new ReturnItem($"ri-{Guid.NewGuid():N}", returnCaseId,
                    orderId, line.OrderItemId, sessionId, line.Quantity, ReturnItemState.Reserved));
            }

            AddTraceAndIdempotency(accepted, submissionKey, request.PayloadHash);
            dbContext.DecisionRecords.Add(new DecisionRecord($"decision-{suffix}", sessionId,
                submissionId, returnCaseId, accepted.RunId, accepted.FinalOutcome,
                "BR-RET-001,BR-RET-002,BR-RET-003,BR-RET-004,BR-RET-006,BR-AUD-001", now));
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return accepted;
        }, cancellationToken);

    public async Task<AiExecutionTrace?> GetTraceAsync(
        string sessionId, string runId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var payload = await dbContext.AiTraces.AsNoTracking()
            .Where(x => x.Id == runId && x.DemoSessionId == sessionId
                && dbContext.DemoSessions.Any(session => session.Id == sessionId && session.ExpiresAt > now))
            .Select(x => x.PayloadJson).SingleOrDefaultAsync(cancellationToken);
        return Deserialize(payload);
    }

    private async Task<AiExecutionTrace> SaveBusinessRejectionAsync(
        ReturnCommitRequest request, string submissionKey, string reasonCode,
        SqliteTransaction transaction, CancellationToken cancellationToken)
    {
        var rejected = request.PreCommitTrace with
        {
            FinalOutcome = "CannotProceed",
            CommitStatus = "Not committed",
            CreatedReturnCaseId = null,
            Stages = request.PreCommitTrace.Stages.Append(new AiStageResult(
                "Commit", "Failed", [reasonCode], ["BR-RET-002", "BR-RET-003", "BR-RET-004"],
                [new SafeEvidence("commitStatus", "Not committed")])).ToArray(),
        };
        AddTraceAndIdempotency(rejected, submissionKey, request.PayloadHash);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return rejected;
    }

    private void AddTraceAndIdempotency(AiExecutionTrace trace, string submissionKey, string payloadHash)
    {
        dbContext.AiTraces.Add(new AiTraceRecord(trace.RunId, trace.DemoSessionId, trace.ReceivedAt,
            JsonSerializer.Serialize(trace, JsonOptions)));
        dbContext.AiRunIdempotencyRecords.Add(new AiRunIdempotencyRecord(
            trace.DemoSessionId, submissionKey, payloadHash, trace.RunId));
    }

    private async Task<(string PayloadHash, AiExecutionTrace Trace)?> FindIdempotencyAsync(
        string sessionId, string submissionKey, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var row = await dbContext.AiRunIdempotencyRecords.AsNoTracking()
            .Where(x => x.DemoSessionId == sessionId && x.SubmissionKey == submissionKey
                && dbContext.DemoSessions.Any(session => session.Id == sessionId && session.ExpiresAt > now))
            .Join(dbContext.AiTraces.AsNoTracking(), x => x.AiExecutionTraceId, x => x.Id,
                (idempotency, trace) => new { idempotency.PayloadHash, trace.PayloadJson })
            .SingleOrDefaultAsync(cancellationToken);
        return row is null ? null : (row.PayloadHash, Deserialize(row.PayloadJson)!);
    }

    private async Task<(string PayloadHash, AiExecutionTrace Trace)?> FindIdempotencyInTransactionAsync(
        string sessionId, string submissionKey, CancellationToken cancellationToken)
    {
        var row = await dbContext.AiRunIdempotencyRecords.AsNoTracking()
            .Where(x => x.DemoSessionId == sessionId && x.SubmissionKey == submissionKey)
            .Join(dbContext.AiTraces.AsNoTracking(), x => x.AiExecutionTraceId, x => x.Id,
                (idempotency, trace) => new { idempotency.PayloadHash, trace.PayloadJson })
            .SingleOrDefaultAsync(cancellationToken);
        return row is null ? null : (row.PayloadHash, Deserialize(row.PayloadJson)!);
    }

    private async Task<T> ExecuteWriteAsync<T>(Func<SqliteTransaction, Task<T>> action, CancellationToken cancellationToken)
    {
        var connection = (SqliteConnection)dbContext.Database.GetDbConnection();
        var openedHere = connection.State != ConnectionState.Open;
        if (openedHere) await connection.OpenAsync(cancellationToken);
        await using (var timeout = connection.CreateCommand())
        {
            timeout.CommandText = "PRAGMA busy_timeout = 5000;";
            await timeout.ExecuteNonQueryAsync(cancellationToken);
        }

        try
        {
            for (var attempt = 1; ; attempt++)
            {
                try
                {
                    await using var transaction = connection.BeginTransaction(deferred: false);
                    await dbContext.Database.UseTransactionAsync(transaction, cancellationToken);
                    var result = await action(transaction);
                    await dbContext.Database.UseTransactionAsync(null, cancellationToken);
                    return result;
                }
                catch (SqliteException exception) when (attempt < MaximumBusyAttempts && exception.SqliteErrorCode is 5 or 6)
                {
                    dbContext.ChangeTracker.Clear();
                    await dbContext.Database.UseTransactionAsync(null, cancellationToken);
                    await Task.Delay(TimeSpan.FromMilliseconds(25 * attempt), cancellationToken);
                }
                catch
                {
                    dbContext.ChangeTracker.Clear();
                    await dbContext.Database.UseTransactionAsync(null, cancellationToken);
                    throw;
                }
            }
        }
        finally
        {
            if (openedHere) await connection.CloseAsync();
        }
    }

    private static AiExecutionTrace? Deserialize(string? payload) => payload is null
        ? null : JsonSerializer.Deserialize<AiExecutionTrace>(payload, JsonOptions);

    private sealed record RequestedLine(string OrderItemId, int Quantity);
}
