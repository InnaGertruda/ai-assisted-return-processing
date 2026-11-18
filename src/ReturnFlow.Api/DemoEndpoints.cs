using System.Security.Cryptography;
using System.Text;
using ReturnFlow.Application;
using ReturnFlow.Infrastructure.Ai;

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
        group.MapGet("/live-status", (LiveModelOptions options) =>
            Results.Ok(new LiveModeStatus(options.Enabled)));

        group.MapPost("/sessions", CreateSessionAsync);
        group.MapGet("/sessions/{sessionId}/orders/{orderId}", GetOrderAsync);
        group.MapGet("/sessions/{sessionId}/return-cases", GetReturnCasesAsync);
        group.MapPost("/sessions/{sessionId}/reset", ResetSessionAsync);
        group.MapPost("/sessions/{sessionId}/ai-runs", RunInterpretationAsync);
        group.MapGet("/sessions/{sessionId}/ai-runs/{runId}", GetInterpretationAsync);

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

    private static async Task<IResult> RunInterpretationAsync(
        string sessionId,
        RunInterpretationRequest request,
        HttpContext httpContext,
        ReturnInterpretationPipeline pipeline,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await pipeline.RunAsync(
                new InterpretReturnMessageRequest(
                    sessionId,
                    request.SelectedOrderId,
                    request.SubmissionKey,
                    request.Message,
                    request.Mode,
                    ClientPartition(httpContext)),
                cancellationToken);
            return Results.Ok(result);
        }
        catch (AiResourceNotFoundException)
        {
            return NotFound();
        }
        catch (AiSubmissionConflictException)
        {
            return Results.Conflict(new DemoApiError(AiSubmissionConflictException.SafeReasonCode));
        }
    }

    private static string ClientPartition(HttpContext httpContext)
    {
        var address = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unavailable";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(address)))
            .ToLowerInvariant();
    }

    private static async Task<IResult> GetInterpretationAsync(
        string sessionId,
        string runId,
        ReturnInterpretationPipeline pipeline,
        CancellationToken cancellationToken)
    {
        try
        {
            return Results.Ok(await pipeline.GetAsync(sessionId, runId, cancellationToken));
        }
        catch (AiResourceNotFoundException)
        {
            return NotFound();
        }
    }
}

public sealed record CreateDemoSessionRequest(string ScenarioId);

public sealed record RunInterpretationRequest(
    string SelectedOrderId,
    string SubmissionKey,
    string Message,
    string Mode);

public sealed record DemoApiError(string ReasonCode);

public sealed record LiveModeStatus(bool Enabled);
