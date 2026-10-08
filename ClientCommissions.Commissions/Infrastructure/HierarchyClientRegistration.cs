using ClientCommissions.Commissions.Application;
using Microsoft.Extensions.Http.Resilience;
using Polly;

namespace ClientCommissions.Commissions.Infrastructure;

public static class HierarchyClientRegistration
{

    public static IHttpClientBuilder AddUsersHierarchyClient(
        this IServiceCollection services, string baseUrl)
    {
        var builder = services.AddHttpClient<IHierarchyProvider, HttpHierarchyProvider>(client =>
        {
            client.BaseAddress = new Uri(baseUrl);
        });

        builder.AddResilienceHandler("users-hierarchy", pipeline =>
        {

            pipeline.AddTimeout(TimeSpan.FromSeconds(10));

            pipeline.AddRetry(new HttpRetryStrategyOptions
            {
                MaxRetryAttempts = 3,
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                Delay = TimeSpan.FromMilliseconds(200),
            });

            pipeline.AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions
            {
                FailureRatio = 0.5,
                MinimumThroughput = 10,
                SamplingDuration = TimeSpan.FromSeconds(30),
                BreakDuration = TimeSpan.FromSeconds(15),
            });

            pipeline.AddTimeout(TimeSpan.FromSeconds(3));
        });

        return builder;
    }
}
