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
        string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        EnsureDatabaseDirectory(connectionString);

        services.AddDbContext<ReturnFlowDbContext>(options => options.UseSqlite(connectionString));
        services.AddScoped<IDemoDataStore, SqliteDemoDataStore>();
        services.AddSingleton<IReturnIntentInterpreter, ReplayReturnIntentInterpreter>();
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
