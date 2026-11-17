using ReturnFlow.Application;
using ReturnFlow.Infrastructure.Ai;
using Xunit;

namespace ReturnFlow.UnitTests;

public sealed class ReplayReturnIntentInterpreterTests
{
    [Fact]
    public async Task Replay_requires_an_exact_versioned_scenario_message_match()
    {
        var interpreter = new ReplayReturnIntentInterpreter();
        var context = new ReturnInterpretationContext(
            "session",
            "S01",
            "order",
            [new DemoOrderItemSummary("trusted-item", "LAMP", "Arc lamp", 3, 1, 0, 2, [])]);

        var matched = await interpreter.InterpretAsync(
            new InterpreterRequest("S01", "I want to return one more lamp.", context),
            default);
        var arbitrary = await interpreter.InterpretAsync(
            new InterpreterRequest("S01", "I want to return a lamp, please.", context),
            default);

        Assert.True(matched.Matched);
        Assert.Contains("trusted-item", matched.RawOutput);
        Assert.False(arbitrary.Matched);
        Assert.Equal("REPLAY_FIXTURE_NOT_MATCHED", arbitrary.FailureReasonCode);
    }
}
