using System.Net;
using System.Net.Http.Json;
using ClientCommissions.Commissions.Application;
using ClientCommissions.Commissions.Domain.Calculations;
using ClientCommissions.Shared.Contracts;

namespace ClientCommissions.Commissions.Infrastructure;

public sealed class HttpHierarchyProvider : IHierarchyProvider
{
    private readonly HttpClient _http;
    private readonly ILogger<HttpHierarchyProvider> _logger;

    public HttpHierarchyProvider(HttpClient http, ILogger<HttpHierarchyProvider> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task<IReadOnlyList<AncestorLink>> GetAncestorsAsync(
        Guid userId, int maxDepth, CancellationToken ct)
    {
        var response = await _http.GetAsync($"/users/{userId}/ancestors?maxDepth={maxDepth}", ct);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            _logger.LogWarning("User {UserId} not found in Users service", userId);
            return Array.Empty<AncestorLink>();
        }

        response.EnsureSuccessStatusCode();

        var contract = await response.Content.ReadFromJsonAsync<List<AncestorContract>>(ct)
                       ?? new List<AncestorContract>();

        return contract.Select(a => new AncestorLink(a.Level, a.UserId)).ToList();
    }
}
