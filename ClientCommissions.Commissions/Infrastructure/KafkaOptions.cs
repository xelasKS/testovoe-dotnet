namespace ClientCommissions.Commissions.Infrastructure;

public sealed class KafkaOptions
{
    public string BootstrapServers { get; set; } = "";
    public string PayoutTopic { get; set; } = "";
}
