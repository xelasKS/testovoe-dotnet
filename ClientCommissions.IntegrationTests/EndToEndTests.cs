using System.Net;
using System.Net.Http.Json;
using ClientCommissions.Commissions.Application;
using ClientCommissions.Commissions.Domain.Entities;
using ClientCommissions.Commissions.Infrastructure;
using ClientCommissions.Users.Infrastructure;
using ClientCommissions.Wallet.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Xunit;

namespace ClientCommissions.IntegrationTests;

public sealed class EndToEndTests : IClassFixture<InfrastructureFixture>
{
    private readonly InfrastructureFixture _infra;

    public EndToEndTests(InfrastructureFixture infra) => _infra = infra;

    [Fact]
    public async Task Event_flows_through_all_services_and_credits_wallet_balances()
    {
        const string topic = "commission-payouts";

        await PrepareSchemaAsync();

        using var users = new ServiceFactory<ClientCommissions.Users.ApiMarker>(new()
        {
            ["ConnectionStrings:DefaultConnection"] = _infra.Conn("clientcommissions_users"),
        });
        var usersClient = users.CreateClient();

        using var wallet = new ServiceFactory<ClientCommissions.Wallet.ApiMarker>(new()
        {
            ["ConnectionStrings:DefaultConnection"] = _infra.Conn("clientcommissions_wallet"),
            ["Kafka:BootstrapServers"] = _infra.Bootstrap,
            ["Kafka:PayoutTopic"] = topic,
            ["Kafka:ConsumerGroup"] = "wallet-e2e",
        });
        var walletClient = wallet.CreateClient();

        using var commissions = new ServiceFactory<ClientCommissions.Commissions.ApiMarker>(new()
            {
                ["ConnectionStrings:DefaultConnection"] = _infra.Conn("clientcommissions"),
                ["Services:Users"] = "http://localhost",
                ["Kafka:BootstrapServers"] = _infra.Bootstrap,
                ["Kafka:PayoutTopic"] = topic,
            },
            services =>
            {
                services.RemoveAll<IHierarchyProvider>();
                services.AddSingleton<IHierarchyProvider>(sp =>
                    new HttpHierarchyProvider(
                        users.CreateClient(),
                        sp.GetRequiredService<ILogger<HttpHierarchyProvider>>()));
            });
        var commissionsClient = commissions.CreateClient();

        var root = await CreateUser(usersClient, null);
        var mid = await CreateUser(usersClient, root);
        var leaf = await CreateUser(usersClient, mid);

        var response = await commissionsClient.PostAsJsonAsync("/events", new
        {
            externalId = Guid.NewGuid(),
            userId = leaf,
            profit = 1000m,
        });
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

        var midBalance = await PollBalanceAsync(walletClient, mid, TimeSpan.FromSeconds(120));
        var rootBalance = await PollBalanceAsync(walletClient, root, TimeSpan.FromSeconds(120));

        Assert.Equal(10m, midBalance);
        Assert.Equal(20m, rootBalance);
    }

    private async Task PrepareSchemaAsync()
    {
        await using (var commissionsDb = new ApplicationContext(
                         Options<ApplicationContext>(_infra.Conn("clientcommissions"))))
        {
            await commissionsDb.Database.MigrateAsync();
            if (!await commissionsDb.SchemaSettings.AnyAsync())
            {
                commissionsDb.SchemaSettings.Add(new SchemaSetting(SchemaType.Linear));
                await commissionsDb.SaveChangesAsync();
            }
        }

        await using (var usersDb = new UsersDbContext(
                         Options<UsersDbContext>(_infra.Conn("clientcommissions_users"))))
        {
            await usersDb.Database.MigrateAsync();
        }

        await using (var walletDb = new WalletDbContext(
                         Options<WalletDbContext>(_infra.Conn("clientcommissions_wallet"))))
        {
            await walletDb.Database.MigrateAsync();
        }
    }

    private static DbContextOptions<TContext> Options<TContext>(string connectionString)
        where TContext : DbContext =>
        new DbContextOptionsBuilder<TContext>().UseNpgsql(connectionString).Options;

    private static async Task<Guid> CreateUser(HttpClient client, Guid? referrerId)
    {
        var response = await client.PostAsJsonAsync("/users", new { id = (Guid?)null, referrerId });
        response.EnsureSuccessStatusCode();
        var created = await response.Content.ReadFromJsonAsync<CreatedUser>();
        return created!.Id;
    }

    private static async Task<decimal?> PollBalanceAsync(HttpClient client, Guid userId, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            var response = await client.GetAsync($"/accounts/{userId}");
            if (response.IsSuccessStatusCode)
            {
                var balance = await response.Content.ReadFromJsonAsync<BalanceDto>();
                if (balance is not null && balance.Balance > 0)
                    return balance.Balance;
            }

            await Task.Delay(TimeSpan.FromSeconds(1));
        }

        return null;
    }

    private sealed record CreatedUser(Guid Id, Guid? ReferrerId);

    private sealed record BalanceDto(Guid UserId, decimal Balance);

    private sealed class ServiceFactory<TMarker>(
        Dictionary<string, string?> settings,
        Action<IServiceCollection>? configureServices = null) : WebApplicationFactory<TMarker>
        where TMarker : class
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureAppConfiguration((_, cfg) => cfg.AddInMemoryCollection(settings));
            if (configureServices is not null)
                builder.ConfigureTestServices(configureServices);
        }
    }
}
