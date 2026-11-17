using System.Reflection;
using System.Text.Json;
using ReturnFlow.Application;

namespace ReturnFlow.Infrastructure.Ai;

public sealed class ReplayReturnIntentInterpreter : IReturnIntentInterpreter
{
    private const string ResourceName =
        "ReturnFlow.Infrastructure.Ai.Fixtures.replay-return-intent-v1.json";

    private readonly IReadOnlyDictionary<string, Fixture> _fixtures;

    public ReplayReturnIntentInterpreter()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Embedded Replay fixture '{ResourceName}' was not found.");
        _fixtures = (JsonSerializer.Deserialize<Fixture[]>(stream, new JsonSerializerOptions(JsonSerializerDefaults.Web))
            ?? throw new InvalidOperationException("Replay fixture catalog is invalid."))
            .ToDictionary(x => x.ScenarioId, StringComparer.Ordinal);
    }

    public Task<InterpreterResult> InterpretAsync(
        InterpreterRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!_fixtures.TryGetValue(request.ScenarioId, out var fixture)
            || !string.Equals(request.Message, fixture.Message, StringComparison.Ordinal))
        {
            return Task.FromResult(new InterpreterResult(
                false,
                null,
                "REPLAY_FIXTURE_NOT_MATCHED"));
        }

        var output = fixture.RawOutput;
        for (var index = 0; index < request.Context.Items.Count; index++)
        {
            output = output.Replace(
                $"{{{{item{index}}}}}",
                request.Context.Items[index].Id,
                StringComparison.Ordinal);
        }

        return Task.FromResult(new InterpreterResult(true, output, null));
    }

    private sealed record Fixture(string ScenarioId, string Message, string RawOutput);
}
