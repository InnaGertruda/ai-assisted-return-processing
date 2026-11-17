using System.Globalization;

namespace ReturnFlow.Api;

internal sealed record DemoSessionRuntimeSettings(TimeSpan Lifetime)
{
    internal const string EnvironmentVariableName = "RETURNFLOW_SESSION_LIFETIME_MINUTES";

    internal static DemoSessionRuntimeSettings FromConfiguration(IConfiguration configuration)
    {
        var rawValue = configuration[EnvironmentVariableName];
        if (!int.TryParse(
                rawValue,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var minutes)
            || minutes is < 1 or > 1440)
        {
            throw new InvalidOperationException(
                $"Environment variable '{EnvironmentVariableName}' must be an integer between 1 and 1440.");
        }

        return new DemoSessionRuntimeSettings(TimeSpan.FromMinutes(minutes));
    }
}
