using System.Security.Cryptography;
using System.Text;
using ReturnFlow.Application;
using ReturnFlow.Domain;

namespace ReturnFlow.Infrastructure.DemoData;

public static class S01ScenarioFixture
{
    public const string ScenarioId = "S01";
    public const string FixtureVersion = "1.0";

    public static DemoScenarioSummary CatalogEntry { get; } = new(
        ScenarioId,
        "Clear additional return",
        "Three fictional lamps were purchased and one is already reserved in a seeded return case.",
        FixtureVersion);

    public static S01FixtureData Create(
        string sessionId,
        DateTimeOffset createdAt,
        DateTimeOffset expiresAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);

        var suffix = CreateStableSuffix(sessionId);
        var orderId = $"ord-s01-{suffix}";
        var orderItemId = $"oi-lamp-{suffix}";
        var submissionId = $"sub-seed-{suffix}";
        var returnCaseId = $"rc-seed-{suffix}";

        return new S01FixtureData(
            new DemoSession(sessionId, ScenarioId, createdAt, expiresAt),
            new DemoOrder(orderId, sessionId, "RF-1050"),
            new OrderItem(orderItemId, orderId, sessionId, "LAMP-ARC", "Arc floor lamp", 3),
            new ReturnSubmission(
                submissionId,
                orderId,
                sessionId,
                "seeded-s01-existing-return",
                "Fictional seeded return: one lamp.",
                SubmissionChannel.SimulatedEmail,
                createdAt),
            new ReturnCase(
                returnCaseId,
                orderId,
                sessionId,
                submissionId,
                ReturnCaseOrigin.SeedFixture,
                createdAt),
            new ReturnItem(
                $"ri-seed-{suffix}",
                returnCaseId,
                orderId,
                orderItemId,
                sessionId,
                1,
                ReturnItemState.Reserved));
    }

    private static string CreateStableSuffix(string sessionId)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(sessionId));
        return Convert.ToHexString(hash)[..32].ToLowerInvariant();
    }
}

public sealed record S01FixtureData(
    DemoSession Session,
    DemoOrder Order,
    OrderItem OrderItem,
    ReturnSubmission Submission,
    ReturnCase ReturnCase,
    ReturnItem ReturnItem);
