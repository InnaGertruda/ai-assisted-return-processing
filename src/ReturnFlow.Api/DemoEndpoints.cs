using ReturnFlow.Application;

namespace ReturnFlow.Api;

public static class DemoEndpoints
{
    public static IEndpointRouteBuilder MapDemoEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/demo");

        group.MapGet("/scenarios", async (
            DemoSessionService service,
            CancellationToken cancellationToken) =>
            Results.Ok(await service.GetScenariosAsync(cancellationToken)));

        group.MapPost("/sessions", CreateSessionAsync);
        group.MapGet("/sessions/{sessionId}/orders/{orderId}", GetOrderAsync);
        group.MapGet("/sessions/{sessionId}/return-cases", GetReturnCasesAsync);
        group.MapPost("/sessions/{sessionId}/reset", ResetSessionAsync);

        return endpoints;
    }

    private static async Task<IResult> CreateSessionAsync(
        CreateDemoSessionRequest request,
        DemoSessionService service,
        CancellationToken cancellationToken)
    {
        try
        {
            var session = await service.CreateSessionAsync(request.ScenarioId, cancellationToken);
            return Results.Created(
                $"/api/demo/sessions/{session.SessionId}/orders/{session.Order.Id}",
                session);
        }
        catch (DemoRequestException exception)
        {
            return Results.BadRequest(new DemoApiError(exception.ReasonCode));
        }
    }

    private static async Task<IResult> GetOrderAsync(
        string sessionId,
        string orderId,
        DemoSessionService service,
        CancellationToken cancellationToken)
    {
        try
        {
            return Results.Ok(await service.GetOrderAsync(
                sessionId,
                orderId,
                cancellationToken));
        }
        catch (DemoResourceNotFoundException)
        {
            return NotFound();
        }
    }

    private static async Task<IResult> GetReturnCasesAsync(
        string sessionId,
        DemoSessionService service,
        CancellationToken cancellationToken)
    {
        try
        {
            return Results.Ok(await service.GetReturnCasesAsync(sessionId, cancellationToken));
        }
        catch (DemoResourceNotFoundException)
        {
            return NotFound();
        }
    }

    private static async Task<IResult> ResetSessionAsync(
        string sessionId,
        DemoSessionService service,
        CancellationToken cancellationToken)
    {
        try
        {
            return Results.Ok(await service.ResetSessionAsync(sessionId, cancellationToken));
        }
        catch (DemoResourceNotFoundException)
        {
            return NotFound();
        }
    }

    private static IResult NotFound() =>
        Results.NotFound(new DemoApiError(DemoResourceNotFoundException.SafeReasonCode));
}

public sealed record CreateDemoSessionRequest(string ScenarioId);

public sealed record DemoApiError(string ReasonCode);
