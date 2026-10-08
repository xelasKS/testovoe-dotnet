using System.Text.Json;
using ClientCommissions.Shared.Contracts;
using Confluent.Kafka;
using Xunit;

namespace ClientCommissions.IntegrationTests;

public sealed class KafkaRoundTripTests : IClassFixture<InfrastructureFixture>
{
    private readonly InfrastructureFixture _infra;

    public KafkaRoundTripTests(InfrastructureFixture infra) => _infra = infra;

    [Fact]
    public async Task Payout_message_round_trips_through_kafka_preserving_key_and_payload()
    {
        const string topic = "roundtrip-test";
        var payout = new PayoutContract(Guid.NewGuid(), Guid.NewGuid(), 42.50m);
        var value = JsonSerializer.Serialize(payout);

        using (var producer = new ProducerBuilder<string, string>(new ProducerConfig
               {
                   BootstrapServers = _infra.Bootstrap,
                   Acks = Acks.All,
                   EnableIdempotence = true,
               }).Build())
        {
            await producer.ProduceAsync(topic, new Message<string, string>
            {
                Key = payout.UserId.ToString(),
                Value = value,
            });
        }

        using var consumer = new ConsumerBuilder<string, string>(new ConsumerConfig
        {
            BootstrapServers = _infra.Bootstrap,
            GroupId = "roundtrip-" + Guid.NewGuid(),
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false,
        }).Build();
        consumer.Subscribe(topic);

        var result = consumer.Consume(TimeSpan.FromSeconds(30));
        consumer.Close();

        Assert.NotNull(result);
        Assert.Equal(payout.UserId.ToString(), result!.Message.Key);
        Assert.Equal(payout, JsonSerializer.Deserialize<PayoutContract>(result.Message.Value));
    }
}
