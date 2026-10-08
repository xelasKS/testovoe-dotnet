using System.Net;
using System.Text;
using ClientCommissions.Commissions.Application;
using ClientCommissions.Commissions.Domain.Calculations;
using ClientCommissions.Commissions.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using Xunit;

namespace ClientCommissions.Commissions.Tests;

public sealed class HttpHierarchyProviderTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    private static IHierarchyProvider BuildProvider(StubHandler handler)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddUsersHierarchyClient("http://users.test")
            .ConfigurePrimaryHttpMessageHandler(() => handler);

        return services.BuildServiceProvider().GetRequiredService<IHierarchyProvider>();
    }

    private static HttpResponseMessage Json(string body) =>
        new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private static HttpResponseMessage Status(HttpStatusCode code) => new(code);

    [Fact]
    public async Task Maps_ancestors_and_calls_correct_url()
    {
        var g1 = Guid.NewGuid();
        var g2 = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var handler = new StubHandler(() => Json(
            $"[{{\"level\":1,\"userId\":\"{g1}\"}},{{\"level\":2,\"userId\":\"{g2}\"}}]"));
        var provider = BuildProvider(handler);

        var chain = await provider.GetAncestorsAsync(userId, 10, Ct);

        Assert.Equal(2, chain.Count);
        Assert.Equal(new AncestorLink(1, g1), chain[0]);
        Assert.Equal(new AncestorLink(2, g2), chain[1]);

        var url = handler.RequestUris[0]!.ToString();
        Assert.Contains($"/users/{userId}/ancestors", url);
        Assert.Contains("maxDepth=10", url);
    }

    [Fact]
    public async Task NotFound_returns_empty_and_is_not_retried()
    {
        var handler = new StubHandler(() => Status(HttpStatusCode.NotFound));
        var provider = BuildProvider(handler);

        var chain = await provider.GetAncestorsAsync(Guid.NewGuid(), 10, Ct);

        Assert.Empty(chain);
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task Transient_failures_are_retried_until_success()
    {
        var handler = new StubHandler(
            () => Status(HttpStatusCode.ServiceUnavailable),
            () => Status(HttpStatusCode.ServiceUnavailable),
            () => Json("[]"));
        var provider = BuildProvider(handler);

        var chain = await provider.GetAncestorsAsync(Guid.NewGuid(), 10, Ct);

        Assert.Empty(chain);
        Assert.Equal(3, handler.Calls);
    }

    [Fact]
    public async Task Persistent_failure_throws_after_retries_exhausted()
    {
        var handler = new StubHandler(() => Status(HttpStatusCode.ServiceUnavailable));
        var provider = BuildProvider(handler);

        await Assert.ThrowsAsync<HttpRequestException>(
            () => provider.GetAncestorsAsync(Guid.NewGuid(), 10, Ct));

        Assert.Equal(4, handler.Calls);
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Queue<Func<HttpResponseMessage>> _responses;

        public int Calls { get; private set; }
        public List<Uri?> RequestUris { get; } = new();

        public StubHandler(params Func<HttpResponseMessage>[] responses)
            => _responses = new Queue<Func<HttpResponseMessage>>(responses);

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            RequestUris.Add(request.RequestUri);

            var factory = _responses.Count > 1 ? _responses.Dequeue() : _responses.Peek();
            return Task.FromResult(factory());
        }
    }
}
