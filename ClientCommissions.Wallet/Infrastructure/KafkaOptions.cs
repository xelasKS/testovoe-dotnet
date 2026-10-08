namespace ClientCommissions.Wallet.Infrastructure;

public sealed class KafkaOptions
{
    public string BootstrapServers { get; set; } = "";
    public string PayoutTopic { get; set; } = "";
    public string DlqTopic { get; set; } = "";
    public string ConsumerGroup { get; set; } = "";
}
