using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using ReturnFlow.Application;
using ReturnFlow.Infrastructure;
using ReturnFlow.Infrastructure.Persistence;

namespace ReturnFlow.IntegrationTests;

public sealed class ReturnFlowApiFactory : WebApplicationFactory<Program>
{
    private readonly string _databasePath = Path.Combine(
        Path.GetTempPath(),
        "returnflow-tests",
        $"{Guid.NewGuid():N}.db");
    private readonly string? _fixedSessionId;

    public ReturnFlowApiFactory()
        : this(fixedSessionId: null)
    {
    }

    internal ReturnFlowApiFactory(string? fixedSessionId)
    {
        _fixedSessionId = fixedSessionId;
    }

    public AdjustableTimeProvider TimeProvider { get; } = new(
        new DateTimeOffset(2026, 11, 17, 12, 0, 0, TimeSpan.Zero));

    internal string DatabasePath => _databasePath;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureLogging(logging => logging.ClearProviders());

        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:ReturnFlow"] =
                    $"Data Source={_databasePath};Foreign Keys=True;Pooling=False",
                ["RETURNFLOW_SESSION_LIFETIME_MINUTES"] = "120",
            });
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<ReturnFlowDbContext>>();
            services.RemoveAll<ReturnFlowDbContext>();
            services.RemoveAll<IDemoDataStore>();
            services.RemoveAll<IReturnFlowDatabaseInitializer>();
            services.AddReturnFlowInfrastructure(
                $"Data Source={_databasePath};Foreign Keys=True;Pooling=False");

            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(TimeProvider);

            if (_fixedSessionId is not null)
            {
                services.RemoveAll<IDemoSessionIdGenerator>();
                services.AddSingleton<IDemoSessionIdGenerator>(
                    new FixedSessionIdGenerator(_fixedSessionId));
            }
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (!disposing)
        {
            return;
        }

        foreach (var path in new[] { _databasePath, $"{_databasePath}-shm", $"{_databasePath}-wal" })
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    private sealed class FixedSessionIdGenerator(string sessionId) : IDemoSessionIdGenerator
    {
        public string Create() => sessionId;
    }
}

public sealed class AdjustableTimeProvider(DateTimeOffset utcNow) : TimeProvider
{
    private DateTimeOffset _utcNow = utcNow;

    public override DateTimeOffset GetUtcNow() => _utcNow;

    public void Advance(TimeSpan duration) => _utcNow = _utcNow.Add(duration);
}
