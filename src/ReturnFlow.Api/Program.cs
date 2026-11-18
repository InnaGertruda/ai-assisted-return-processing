using ReturnFlow.Api;
using ReturnFlow.Application;
using ReturnFlow.Infrastructure;
using ReturnFlow.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("ReturnFlow")
    ?? throw new InvalidOperationException("Connection string 'ReturnFlow' is required.");
var liveModelOptions = LiveModelRuntimeSettings.FromConfiguration(builder.Configuration);

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IDemoSessionIdGenerator, GuidDemoSessionIdGenerator>();
builder.Services.AddSingleton(serviceProvider =>
    DemoSessionRuntimeSettings.FromConfiguration(
        serviceProvider.GetRequiredService<IConfiguration>()));
builder.Services.AddReturnFlowInfrastructure(connectionString, liveModelOptions);
builder.Services.AddScoped(serviceProvider => new DemoSessionService(
    serviceProvider.GetRequiredService<IDemoDataStore>(),
    serviceProvider.GetRequiredService<IDemoSessionIdGenerator>(),
    serviceProvider.GetRequiredService<TimeProvider>(),
    serviceProvider.GetRequiredService<DemoSessionRuntimeSettings>().Lifetime));
builder.Services.AddScoped<ReturnInterpretationPipeline>();

var app = builder.Build();
_ = app.Services.GetRequiredService<DemoSessionRuntimeSettings>();

await using (var scope = app.Services.CreateAsyncScope())
{
    await scope.ServiceProvider
        .GetRequiredService<IReturnFlowDatabaseInitializer>()
        .InitializeAsync();
}

app.MapGet("/health", () => Results.Ok(new HealthResponse("Healthy", "ReturnFlow.Api")))
    .WithName("Health");
app.MapDemoEndpoints();

app.Run();

public sealed record HealthResponse(string Status, string Service);

public partial class Program;
