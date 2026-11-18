using Microsoft.Extensions.DependencyInjection;
using ReturnFlow.Application;
using ReturnFlow.Infrastructure.Ai;
using ReturnFlow.Infrastructure.Persistence;
using Xunit;

namespace ReturnFlow.IntegrationTests;

public sealed class LiveUsageLimitTests
{
    [Fact]
    public async Task Persistent_limits_reject_session_burst_and_global_daily_overuse()
    {
        using var factory = new ReturnFlowApiFactory();
        _ = factory.CreateClient();
        var options = new LiveModelOptions(
            true, "test-secret", "test-model", TimeSpan.FromSeconds(5), 500,
            SessionRequestsPerMinute: 1,
            SessionRequestsPerDay: 5,
            ClientRequestsPerMinute: 10,
            GlobalRequestsPerDay: 2);
        var now = factory.TimeProvider.GetUtcNow();

        Assert.True((await AcquireAsync(factory, options, "session-a", "client-a", now)).Allowed);

        var burst = await AcquireAsync(factory, options, "session-a", "client-a", now);
        Assert.False(burst.Allowed);
        Assert.Equal("LIVE_SESSION_MINUTE_LIMIT_REACHED", burst.FailureReasonCode);

        now = now.AddMinutes(2);
        Assert.True((await AcquireAsync(factory, options, "session-a", "client-a", now)).Allowed);

        var global = await AcquireAsync(factory, options, "session-b", "client-b", now);
        Assert.False(global.Allowed);
        Assert.Equal("LIVE_GLOBAL_DAILY_LIMIT_REACHED", global.FailureReasonCode);

        var nextDay = now.AddDays(1);
        Assert.True((await AcquireAsync(factory, options, "session-b", "client-b", nextDay)).Allowed);
    }

    [Fact]
    public async Task Client_partition_limit_applies_across_sessions()
    {
        using var factory = new ReturnFlowApiFactory();
        _ = factory.CreateClient();
        var options = new LiveModelOptions(
            true, "test-secret", "test-model", TimeSpan.FromSeconds(5), 500,
            SessionRequestsPerMinute: 5,
            SessionRequestsPerDay: 5,
            ClientRequestsPerMinute: 1,
            GlobalRequestsPerDay: 20);
        var now = factory.TimeProvider.GetUtcNow();

        Assert.True((await AcquireAsync(factory, options, "session-c", "shared-client", now)).Allowed);
        var result = await AcquireAsync(factory, options, "session-d", "shared-client", now);

        Assert.False(result.Allowed);
        Assert.Equal("LIVE_CLIENT_MINUTE_LIMIT_REACHED", result.FailureReasonCode);
    }

    private async Task<LiveUsageDecision> AcquireAsync(
        ReturnFlowApiFactory factory,
        LiveModelOptions options,
        string sessionId,
        string clientPartition,
        DateTimeOffset now)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var limiter = new SqliteLiveUsageLimiter(
            scope.ServiceProvider.GetRequiredService<ReturnFlowDbContext>(),
            options);
        return await limiter.TryAcquireAsync(sessionId, clientPartition, now, CancellationToken.None);
    }
}
