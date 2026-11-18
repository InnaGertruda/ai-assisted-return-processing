using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ReturnFlow.Application;
using ReturnFlow.Infrastructure.Ai;
using ReturnFlow.Infrastructure.DemoData;
using ReturnFlow.Infrastructure.Persistence;

namespace ReturnFlow.Infrastructure;

public static class InfrastructureRegistration
{
    public static IServiceCollection AddReturnFlowInfrastructure(
        this IServiceCollection services,
        string connectionString,
        LiveModelOptions? liveModelOptions = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        EnsureDatabaseDirectory(connectionString);
        var resolvedLiveOptions = liveModelOptions ?? LiveModelOptions.Disabled;
        resolvedLiveOptions.Validate();

        services.AddDbContext<ReturnFlowDbContext>(options => options.UseSqlite(connectionString));
        services.AddScoped<IDemoDataStore, SqliteDemoDataStore>();
        services.AddSingleton(resolvedLiveOptions);
        services.AddSingleton(new HttpClient
        {
            MaxResponseContentBufferSize = 64 * 1024,
        });
        services.AddSingleton<ReplayReturnIntentInterpreter>();
        services.AddScoped<ILiveUsageLimiter, SqliteLiveUsageLimiter>();
        services.AddScoped<GroqReturnIntentInterpreter>();
        services.AddScoped<IReturnIntentInterpreter, ModeRoutingReturnIntentInterpreter>();
        services.AddScoped<IReturnInterpretationStore, SqliteReturnInterpretationStore>();
        services.AddScoped<IReturnFlowDatabaseInitializer, ReturnFlowDatabaseInitializer>();
        return services;
    }

    private static void EnsureDatabaseDirectory(string connectionString)
    {
        var dataSource = new SqliteConnectionStringBuilder(connectionString).DataSource;
        if (string.IsNullOrWhiteSpace(dataSource) || dataSource == ":memory:")
        {
            return;
        }

        var fullPath = Path.GetFullPath(dataSource);
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }
}
