using Npgsql;
using Testcontainers.Kafka;
using Testcontainers.PostgreSql;
using Xunit;

namespace ClientCommissions.IntegrationTests;

public sealed class InfrastructureFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .WithDatabase("postgres")
        .Build();

    private readonly KafkaContainer _kafka = new KafkaBuilder()
        .WithImage("confluentinc/cp-kafka:7.6.1")
        .Build();

    public string Bootstrap => _kafka.GetBootstrapAddress();

    public string Conn(string database) =>
        $"Host={_postgres.Hostname};Port={_postgres.GetMappedPublicPort(5432)};" +
        $"Database={database};Username=postgres;Password=postgres";

    public async Task InitializeAsync()
    {
        await Task.WhenAll(_postgres.StartAsync(), _kafka.StartAsync());

        await using var conn = new NpgsqlConnection(Conn("postgres"));
        await conn.OpenAsync();
        foreach (var db in new[] { "clientcommissions", "clientcommissions_users", "clientcommissions_wallet" })
        {
            await using var cmd = new NpgsqlCommand($"CREATE DATABASE \"{db}\"", conn);
            await cmd.ExecuteNonQueryAsync();
        }
    }

    public async Task DisposeAsync()
    {
        await _postgres.DisposeAsync();
        await _kafka.DisposeAsync();
    }
}
