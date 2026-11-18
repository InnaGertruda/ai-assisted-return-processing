using ReturnFlow.Application;

namespace ReturnFlow.Infrastructure.Ai;

public sealed class ModeRoutingReturnIntentInterpreter(
    ReplayReturnIntentInterpreter replay,
    GroqReturnIntentInterpreter live) : IReturnIntentInterpreter
{
    public Task<InterpreterResult> InterpretAsync(
        InterpreterRequest request,
        CancellationToken cancellationToken) =>
        string.Equals(request.Mode, "Live", StringComparison.OrdinalIgnoreCase)
            ? live.InterpretAsync(request, cancellationToken)
            : replay.InterpretAsync(request, cancellationToken);
}
