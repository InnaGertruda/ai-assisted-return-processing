using System.Globalization;
using ReturnFlow.Infrastructure.Ai;

namespace ReturnFlow.Api;

internal static class LiveModelRuntimeSettings
{
    internal static LiveModelOptions FromConfiguration(IConfiguration configuration)
    {
        var options = new LiveModelOptions(
            ReadBoolean(configuration, "LIVE_MODEL_ENABLED", false),
            configuration["LIVE_MODEL_API_KEY"] ?? string.Empty,
            configuration["LIVE_MODEL_ID"] ?? string.Empty,
            TimeSpan.FromSeconds(ReadInteger(configuration, "LIVE_MODEL_TIMEOUT_SECONDS", 10)),
            ReadInteger(configuration, "LIVE_MODEL_MAX_OUTPUT_TOKENS", 1024),
            ReadInteger(configuration, "LIVE_MODEL_SESSION_REQUESTS_PER_MINUTE", 2),
            ReadInteger(configuration, "LIVE_MODEL_SESSION_REQUESTS_PER_DAY", 10),
            ReadInteger(configuration, "LIVE_MODEL_CLIENT_REQUESTS_PER_MINUTE", 4),
            ReadInteger(configuration, "LIVE_MODEL_GLOBAL_REQUESTS_PER_DAY", 50));
        options.Validate();
        return options;
    }

    private static bool ReadBoolean(IConfiguration configuration, string name, bool fallback)
    {
        var raw = configuration[name];
        if (string.IsNullOrWhiteSpace(raw))
        {
            return fallback;
        }

        return bool.TryParse(raw, out var value)
            ? value
            : throw new InvalidOperationException($"Environment variable '{name}' must be true or false.");
    }

    private static int ReadInteger(IConfiguration configuration, string name, int fallback)
    {
        var raw = configuration[name];
        if (string.IsNullOrWhiteSpace(raw))
        {
            return fallback;
        }

        return int.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out var value)
            ? value
            : throw new InvalidOperationException($"Environment variable '{name}' must be an integer.");
    }
}
