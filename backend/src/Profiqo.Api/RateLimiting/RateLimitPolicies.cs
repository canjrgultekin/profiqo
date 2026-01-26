using System.Threading.RateLimiting;

using Microsoft.AspNetCore.RateLimiting;

namespace Profiqo.Api.RateLimiting;

internal static class RateLimitPolicies
{
    public const string Global = "global";

    public static RateLimiterOptions Create(int rps)
    {
        return new RateLimiterOptions
        {
            GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(_ =>
                RateLimitPartition.GetTokenBucketLimiter(
                    partitionKey: "global",
                    factory: _ => new TokenBucketRateLimiterOptions
                    {
                        TokenLimit = rps,
                        TokensPerPeriod = rps,
                        ReplenishmentPeriod = TimeSpan.FromSeconds(1),
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = rps * 2,
                        AutoReplenishment = true
                    }))
        };
    }
}