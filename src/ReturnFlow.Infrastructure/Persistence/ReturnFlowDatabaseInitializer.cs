using Microsoft.EntityFrameworkCore;

namespace ReturnFlow.Infrastructure.Persistence;

public interface IReturnFlowDatabaseInitializer
{
    Task InitializeAsync(CancellationToken cancellationToken = default);
}

public sealed class ReturnFlowDatabaseInitializer(ReturnFlowDbContext dbContext)
    : IReturnFlowDatabaseInitializer
{
    public Task InitializeAsync(CancellationToken cancellationToken = default) =>
        dbContext.Database.MigrateAsync(cancellationToken);
}
