using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using ReturnFlow.Application;
using ReturnFlow.Infrastructure.Ai;
using Xunit;

namespace ReturnFlow.UnitTests;

public sealed class GroqReturnIntentInterpreterTests
{
    private static readonly ReturnInterpretationContext Context = new(
        "session-1",
        "S01",
        "order-1",
        [new DemoOrderItemSummary("item-1", "LAMP", "Arc lamp", 3, 1, 0, 2, ["case-1"])]);

    [Fact]
    public async Task Valid_structured_response_returns_raw_proposal_and_bounded_request()
    {
        const string proposal =
            """
            {"schemaVersion":"1.0","intent":"CreateReturn","items":[{"candidateOrderItemId":"item-1","productMention":"lamp","requestedQuantity":1,"reason":"Unspecified","evidenceQuotes":["one lamp"]}],"ambiguities":[],"requiresClarification":false}
            """;
        var handler = new RecordingHandler(_ => JsonResponse(new
        {
            choices = new[] { new { message = new { content = proposal } } },
        }));
        var interpreter = Create(handler);

        var result = await interpreter.InterpretAsync(Request(), CancellationToken.None);

        Assert.True(result.Matched);
        Assert.Equal(proposal, result.RawOutput);
        Assert.Equal(AiContractVersions.LivePrompt, result.PromptVersion);
        Assert.Single(handler.Requests);
        Assert.Equal("Bearer", handler.Requests[0].Authorization?.Scheme);
        Assert.Equal("test-secret", handler.Requests[0].Authorization?.Parameter);
        using var sent = JsonDocument.Parse(handler.Requests[0].Body);
        Assert.Equal(500, sent.RootElement.GetProperty("max_completion_tokens").GetInt32());
        Assert.Equal("low", sent.RootElement.GetProperty("reasoning_effort").GetString());
        Assert.False(sent.RootElement.GetProperty("include_reasoning").GetBoolean());
        Assert.Equal("json_schema", sent.RootElement.GetProperty("response_format").GetProperty("type").GetString());
        Assert.DoesNotContain("maxItems", handler.Requests[0].Body, StringComparison.Ordinal);
        Assert.DoesNotContain("minimum", handler.Requests[0].Body, StringComparison.Ordinal);
        Assert.Contains("Return only one JSON object", handler.Requests[0].Body, StringComparison.Ordinal);
        Assert.False(sent.RootElement.TryGetProperty("tools", out _));
    }

    [Fact]
    public async Task Disabled_mode_never_reserves_usage_or_calls_provider()
    {
        var handler = new RecordingHandler(_ => throw new InvalidOperationException("Must not call provider."));
        var limiter = new FakeLimiter();
        var interpreter = Create(handler, LiveModelOptions.Disabled, limiter);

        var result = await interpreter.InterpretAsync(Request(), CancellationToken.None);

        Assert.Equal("LIVE_MODE_DISABLED", result.FailureReasonCode);
        Assert.Equal(0, limiter.CallCount);
        Assert.Empty(handler.Requests);
    }

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests, "LIVE_PROVIDER_RATE_LIMITED")]
    [InlineData(HttpStatusCode.ServiceUnavailable, "LIVE_PROVIDER_UNAVAILABLE")]
    [InlineData(HttpStatusCode.Unauthorized, "LIVE_PROVIDER_AUTHENTICATION_FAILED")]
    public async Task Provider_errors_are_safe_failures_without_retry(
        HttpStatusCode statusCode,
        string expectedReason)
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(statusCode));
        var interpreter = Create(handler);

        var result = await interpreter.InterpretAsync(Request(), CancellationToken.None);

        Assert.False(result.Matched);
        Assert.Equal(expectedReason, result.FailureReasonCode);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task Malformed_provider_envelope_is_rejected()
    {
        var handler = new RecordingHandler(_ => JsonResponse(new { unexpected = true }));

        var result = await Create(handler).InterpretAsync(Request(), CancellationToken.None);

        Assert.Equal("LIVE_PROVIDER_RESPONSE_INVALID", result.FailureReasonCode);
    }

    [Fact]
    public async Task Malformed_provider_json_is_rejected()
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("not-json", Encoding.UTF8, "application/json"),
        });

        var result = await Create(handler).InterpretAsync(Request(), CancellationToken.None);

        Assert.Equal("LIVE_PROVIDER_RESPONSE_INVALID", result.FailureReasonCode);
    }

    [Fact]
    public async Task Provider_timeout_is_reported_without_retry()
    {
        var handler = new RecordingHandler(async (_, cancellationToken) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        var options = EnabledOptions() with { Timeout = TimeSpan.FromMilliseconds(20) };
        var interpreter = Create(handler, options);

        var result = await interpreter.InterpretAsync(Request(), CancellationToken.None);

        Assert.Equal("LIVE_PROVIDER_TIMEOUT", result.FailureReasonCode);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task Local_usage_rejection_prevents_provider_call()
    {
        var handler = new RecordingHandler(_ => throw new InvalidOperationException("Must not call provider."));
        var limiter = new FakeLimiter(new LiveUsageDecision(false, "LIVE_GLOBAL_DAILY_LIMIT_REACHED"));

        var result = await Create(handler, EnabledOptions(), limiter)
            .InterpretAsync(Request(), CancellationToken.None);

        Assert.Equal("LIVE_GLOBAL_DAILY_LIMIT_REACHED", result.FailureReasonCode);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public void Enabled_configuration_requires_secret_model_and_valid_bounds()
    {
        var missingSecret = EnabledOptions() with { ApiKey = string.Empty };
        var invalidTokens = EnabledOptions() with { MaximumOutputTokens = 10 };

        Assert.Throws<InvalidOperationException>(missingSecret.Validate);
        Assert.Throws<InvalidOperationException>(invalidTokens.Validate);
    }

    private static GroqReturnIntentInterpreter Create(
        HttpMessageHandler handler,
        LiveModelOptions? options = null,
        ILiveUsageLimiter? limiter = null) => new(
            new HttpClient(handler),
            options ?? EnabledOptions(),
            limiter ?? new FakeLimiter(),
            TimeProvider.System);

    private static LiveModelOptions EnabledOptions() => new(
        true, "test-secret", "configured-test-model", TimeSpan.FromSeconds(2), 500, 2, 10, 4, 50);

    private static InterpreterRequest Request() => new(
        "S01", "I want to return one lamp.", Context, "Live", "client-1");

    private static HttpResponseMessage JsonResponse(object value) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(JsonSerializer.Serialize(value), Encoding.UTF8, "application/json"),
    };

    private sealed class FakeLimiter(LiveUsageDecision? decision = null) : ILiveUsageLimiter
    {
        public int CallCount { get; private set; }

        public Task<LiveUsageDecision> TryAcquireAsync(
            string sessionId,
            string clientPartition,
            DateTimeOffset now,
            CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(decision ?? new LiveUsageDecision(true));
        }
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _respond;

        public RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> respond)
            : this((request, _) => Task.FromResult(respond(request)))
        {
        }

        public RecordingHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond)
        {
            _respond = respond;
        }

        public List<RecordedRequest> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests.Add(new RecordedRequest(
                request.Headers.Authorization,
                request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken)));
            return await _respond(request, cancellationToken);
        }
    }

    private sealed record RecordedRequest(AuthenticationHeaderValue? Authorization, string Body);
}
