using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ReturnFlow.Application;

namespace ReturnFlow.Infrastructure.Ai;

public sealed class GroqReturnIntentInterpreter(
    HttpClient httpClient,
    LiveModelOptions options,
    ILiveUsageLimiter usageLimiter,
    TimeProvider timeProvider)
{
    private static readonly Uri CompletionsUri = new(
        "https://api.groq.com/openai/v1/chat/completions");

    public async Task<InterpreterResult> InterpretAsync(
        InterpreterRequest request,
        CancellationToken cancellationToken)
    {
        if (!options.Enabled)
        {
            return Failed("LIVE_MODE_DISABLED");
        }

        LiveUsageDecision usage;
        try
        {
            usage = await usageLimiter.TryAcquireAsync(
                request.Context.DemoSessionId,
                request.ClientPartition,
                timeProvider.GetUtcNow(),
                cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return Failed("LIVE_USAGE_LIMIT_UNAVAILABLE");
        }
        if (!usage.Allowed)
        {
            return Failed(usage.FailureReasonCode ?? "LIVE_USAGE_LIMIT_REACHED");
        }

        using var message = new HttpRequestMessage(HttpMethod.Post, CompletionsUri)
        {
            Content = JsonContent.Create(CreateProviderRequest(request)),
        };
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.ApiKey);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(options.Timeout);
        try
        {
            using var response = await httpClient.SendAsync(
                message,
                HttpCompletionOption.ResponseContentRead,
                timeout.Token);
            if (!response.IsSuccessStatusCode)
            {
                return Failed(response.StatusCode switch
                {
                    HttpStatusCode.TooManyRequests => "LIVE_PROVIDER_RATE_LIMITED",
                    HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden =>
                        "LIVE_PROVIDER_AUTHENTICATION_FAILED",
                    HttpStatusCode.ServiceUnavailable or HttpStatusCode.BadGateway
                        or HttpStatusCode.InternalServerError => "LIVE_PROVIDER_UNAVAILABLE",
                    _ => "LIVE_PROVIDER_REQUEST_FAILED",
                });
            }

            var responseJson = await response.Content.ReadAsStringAsync(timeout.Token);
            using var document = JsonDocument.Parse(responseJson);
            var content = document.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();
            return string.IsNullOrWhiteSpace(content)
                ? Failed("LIVE_PROVIDER_RESPONSE_INVALID")
                : new InterpreterResult(true, content, null, AiContractVersions.LivePrompt);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Failed("LIVE_PROVIDER_TIMEOUT");
        }
        catch (HttpRequestException)
        {
            return Failed("LIVE_PROVIDER_UNAVAILABLE");
        }
        catch (Exception exception) when (exception is
            JsonException or InvalidOperationException or KeyNotFoundException or IndexOutOfRangeException)
        {
            return Failed("LIVE_PROVIDER_RESPONSE_INVALID");
        }
    }

    private static InterpreterResult Failed(string reasonCode) =>
        new(false, null, reasonCode, AiContractVersions.LivePrompt);

    private object CreateProviderRequest(InterpreterRequest request) => new
    {
        model = options.ModelId,
        messages = new object[]
        {
            new
            {
                role = "system",
                content =
                    "Interpret the fictional customer return request as an untrusted proposal only. " +
                    "Never authorize refunds, payments, tools, database changes, or policy exceptions. " +
                    "Return only one JSON object that exactly matches the supplied schema, with every required field present and no markdown. " +
                    "Use only the supplied order-item IDs. Evidence quotes must be exact substrings of the customer message. " +
                    "When an item, quantity, or intent is unclear, use null for unresolved nullable fields, add a short ambiguity, " +
                    "set requiresClarification to true, and do not invent an identifier or quantity.",
            },
            new
            {
                role = "user",
                content = JsonSerializer.Serialize(new
                {
                    customerMessage = request.Message,
                    trustedOrderContext = request.Context.Items.Select(item => new
                    {
                        orderItemId = item.Id,
                        item.ProductCode,
                        item.ProductName,
                        item.PurchasedQuantity,
                        item.AvailableQuantity,
                    }),
                }),
            },
        },
        response_format = new
        {
            type = "json_schema",
            json_schema = new
            {
                name = "return_intent_proposal_v1",
                strict = true,
                schema = ProposalSchema(),
            },
        },
        max_completion_tokens = options.MaximumOutputTokens,
        temperature = 0.1,
        reasoning_effort = "low",
        include_reasoning = false,
        stream = false,
    };

    private static object ProposalSchema() => new
    {
        type = "object",
        additionalProperties = false,
        properties = new Dictionary<string, object>
        {
            ["schemaVersion"] = new { type = "string", @enum = new[] { AiContractVersions.Schema } },
            ["intent"] = new { type = "string", @enum = Enum.GetNames<ReturnIntent>() },
            ["items"] = new
            {
                type = "array",
                items = new
                {
                    type = "object",
                    additionalProperties = false,
                    properties = new Dictionary<string, object>
                    {
                        ["candidateOrderItemId"] = new { type = new[] { "string", "null" } },
                        ["productMention"] = new { type = new[] { "string", "null" } },
                        ["requestedQuantity"] = new { type = new[] { "integer", "null" } },
                        ["reason"] = new { type = "string", @enum = Enum.GetNames<ReturnReason>() },
                        ["evidenceQuotes"] = new { type = "array", items = new { type = "string" } },
                    },
                    required = new[]
                    {
                        "candidateOrderItemId", "productMention", "requestedQuantity", "reason", "evidenceQuotes",
                    },
                },
            },
            ["ambiguities"] = new { type = "array", items = new { type = "string" } },
            ["requiresClarification"] = new { type = "boolean" },
        },
        required = new[] { "schemaVersion", "intent", "items", "ambiguities", "requiresClarification" },
    };
}
