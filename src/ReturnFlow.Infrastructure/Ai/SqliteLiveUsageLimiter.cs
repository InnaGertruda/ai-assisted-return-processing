using System.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ReturnFlow.Application;
using ReturnFlow.Domain;
using ReturnFlow.Infrastructure.Persistence;

namespace ReturnFlow.Infrastructure.Ai;

public sealed class SqliteLiveUsageLimiter(
    ReturnFlowDbContext dbContext,
    LiveModelOptions options) : ILiveUsageLimiter
{
    public async Task<LiveUsageDecision> TryAcquireAsync(
        string sessionId,
        string clientPartition,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var connection = (SqliteConnection)dbContext.Database.GetDbConnection();
        var openedHere = connection.State != ConnectionState.Open;
        if (openedHere)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var transaction = connection.BeginTransaction(deferred: false);
            await dbContext.Database.UseTransactionAsync(transaction, cancellationToken);
            var startOfMinute = now.AddMinutes(-1);
            var startOfDay = new DateTimeOffset(now.UtcDateTime.Date, TimeSpan.Zero);

            await dbContext.LiveModelUsageRecords
                .Where(x => x.RequestedAt < startOfDay)
                .ExecuteDeleteAsync(cancellationToken);

            if (await dbContext.LiveModelUsageRecords.CountAsync(
                    x => x.DemoSessionId == sessionId && x.RequestedAt >= startOfMinute,
                    cancellationToken) >= options.SessionRequestsPerMinute)
            {
                return await RejectAsync(transaction, "LIVE_SESSION_MINUTE_LIMIT_REACHED", cancellationToken);
            }

            if (await dbContext.LiveModelUsageRecords.CountAsync(
                    x => x.DemoSessionId == sessionId && x.RequestedAt >= startOfDay,
                    cancellationToken) >= options.SessionRequestsPerDay)
            {
                return await RejectAsync(transaction, "LIVE_SESSION_DAILY_LIMIT_REACHED", cancellationToken);
            }

            if (await dbContext.LiveModelUsageRecords.CountAsync(
                    x => x.ClientPartition == clientPartition && x.RequestedAt >= startOfMinute,
                    cancellationToken) >= options.ClientRequestsPerMinute)
            {
                return await RejectAsync(transaction, "LIVE_CLIENT_MINUTE_LIMIT_REACHED", cancellationToken);
            }

            if (await dbContext.LiveModelUsageRecords.CountAsync(
                    x => x.RequestedAt >= startOfDay,
                    cancellationToken) >= options.GlobalRequestsPerDay)
            {
                return await RejectAsync(transaction, "LIVE_GLOBAL_DAILY_LIMIT_REACHED", cancellationToken);
            }

            dbContext.LiveModelUsageRecords.Add(new LiveModelUsageRecord(
                $"liveuse-{Guid.NewGuid():N}",
                sessionId,
                clientPartition,
                now));
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            await dbContext.Database.UseTransactionAsync(null, cancellationToken);
            return new LiveUsageDecision(true);
        }
        finally
        {
            await dbContext.Database.UseTransactionAsync(null, CancellationToken.None);
            dbContext.ChangeTracker.Clear();
            if (openedHere)
            {
                await connection.CloseAsync();
            }
        }
    }

    private async Task<LiveUsageDecision> RejectAsync(
        SqliteTransaction transaction,
        string reasonCode,
        CancellationToken cancellationToken)
    {
        await transaction.CommitAsync(cancellationToken);
        await dbContext.Database.UseTransactionAsync(null, cancellationToken);
        return new LiveUsageDecision(false, reasonCode);
    }
}
