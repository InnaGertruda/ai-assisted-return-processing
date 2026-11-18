namespace ReturnFlow.Infrastructure.Ai;

public sealed record LiveModelOptions(
    bool Enabled,
    string ApiKey,
    string ModelId,
    TimeSpan Timeout,
    int MaximumOutputTokens,
    int SessionRequestsPerMinute,
    int SessionRequestsPerDay,
    int ClientRequestsPerMinute,
    int GlobalRequestsPerDay)
{
    public static LiveModelOptions Disabled { get; } = new(
        false, string.Empty, string.Empty, TimeSpan.FromSeconds(10), 1024, 2, 10, 4, 50);

    public void Validate()
    {
        if (Timeout < TimeSpan.FromSeconds(1) || Timeout > TimeSpan.FromSeconds(30))
        {
            throw new InvalidOperationException("Live model timeout must be between 1 and 30 seconds.");
        }

        if (MaximumOutputTokens is < 64 or > 2_000
            || SessionRequestsPerMinute is < 1 or > 60
            || SessionRequestsPerDay is < 1 or > 1_000
            || ClientRequestsPerMinute is < 1 or > 120
            || GlobalRequestsPerDay is < 1 or > 100_000)
        {
            throw new InvalidOperationException("Live model usage limits are outside the supported range.");
        }

        if (Enabled && (string.IsNullOrWhiteSpace(ApiKey) || string.IsNullOrWhiteSpace(ModelId)))
        {
            throw new InvalidOperationException(
                "Live mode requires backend LIVE_MODEL_API_KEY and LIVE_MODEL_ID configuration.");
        }
    }
}
