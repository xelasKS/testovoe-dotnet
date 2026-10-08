using System.Text.Json;
using System.Threading.Channels;
using ClientCommissions.Shared.Contracts;
using ClientCommissions.Wallet.Application;
using ClientCommissions.Wallet.Infrastructure;
using Confluent.Kafka;
using Microsoft.Extensions.Options;

namespace ClientCommissions.Wallet.Workers;

public sealed class PayoutConsumerWorker : BackgroundService
{
    private const int ChannelCapacity = 100;
    private const int MaxAttempts = 3;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IProducer<string, string> _dlqProducer;
    private readonly KafkaOptions _kafka;
    private readonly ILogger<PayoutConsumerWorker> _logger;

    public PayoutConsumerWorker(
        IServiceScopeFactory scopeFactory,
        IProducer<string, string> dlqProducer,
        IOptions<KafkaOptions> kafka,
        ILogger<PayoutConsumerWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _dlqProducer = dlqProducer;
        _kafka = kafka.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var channel = Channel.CreateBounded<ConsumeResult<string, string>>(
            new BoundedChannelOptions(ChannelCapacity)
            {
                SingleReader = true,
                SingleWriter = true,
                FullMode = BoundedChannelFullMode.Wait,
            });

        var config = new ConsumerConfig
        {
            BootstrapServers = _kafka.BootstrapServers,
            GroupId = _kafka.ConsumerGroup,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = true,
            EnableAutoOffsetStore = false,
        };

        using var consumer = new ConsumerBuilder<string, string>(config).Build();
        consumer.Subscribe(_kafka.PayoutTopic);
        _logger.LogInformation("Wallet consumer subscribed to {Topic}", _kafka.PayoutTopic);

        var pollLoop = Task.Run(() => PollAsync(consumer, channel.Writer, stoppingToken), stoppingToken);

        try
        {
            await foreach (var result in channel.Reader.ReadAllAsync(stoppingToken))
                await ProcessAsync(consumer, result, stoppingToken);
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            await pollLoop;
            consumer.Close();
        }
    }

    private async Task PollAsync(
        IConsumer<string, string> consumer,
        ChannelWriter<ConsumeResult<string, string>> writer,
        CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                ConsumeResult<string, string> result;
                try
                {
                    result = consumer.Consume(ct);
                }
                catch (ConsumeException e)
                {
                    _logger.LogError(e, "Kafka consume error");
                    continue;
                }

                if (result is not null)
                    await writer.WriteAsync(result, ct);
            }
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            writer.Complete();
        }
    }

    private async Task ProcessAsync(
        IConsumer<string, string> consumer,
        ConsumeResult<string, string> result,
        CancellationToken ct)
    {
        PayoutContract? payout = null;
        try
        {
            payout = JsonSerializer.Deserialize<PayoutContract>(result.Message.Value);
        }
        catch (JsonException)
        {
        }

        if (payout is null)
        {
            _logger.LogError("Unparseable payout message, sending to DLQ");
            await DeadLetterAsync(consumer, result, ct);
            return;
        }

        using (_logger.BeginScope(new Dictionary<string, object> { ["CommissionId"] = payout.CommissionId }))
        {
            for (var attempt = 1; attempt <= MaxAttempts; attempt++)
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var wallet = scope.ServiceProvider.GetRequiredService<IWalletService>();
                    await wallet.ApplyPayoutAsync(payout.CommissionId, payout.UserId, payout.Amount, ct);

                    consumer.StoreOffset(result);
                    _logger.LogInformation("Payout consumed: user {UserId}, amount {Amount}",
                        payout.UserId, payout.Amount);
                    return;
                }
                catch (Exception e) when (attempt < MaxAttempts)
                {
                    _logger.LogWarning(e, "Payout processing failed (attempt {Attempt}/{Max}), retrying",
                        attempt, MaxAttempts);
                    await Task.Delay(TimeSpan.FromMilliseconds(200 * attempt), ct);
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "Payout poisoned after {Max} attempts, sending to DLQ", MaxAttempts);
                    await DeadLetterAsync(consumer, result, ct);
                    return;
                }
            }
        }
    }
    
    private async Task DeadLetterAsync(
        IConsumer<string, string> consumer,
        ConsumeResult<string, string> result,
        CancellationToken ct)
    {
        await _dlqProducer.ProduceAsync(
            _kafka.DlqTopic,
            new Message<string, string> { Key = result.Message.Key, Value = result.Message.Value },
            ct);
        consumer.StoreOffset(result);
    }
}
