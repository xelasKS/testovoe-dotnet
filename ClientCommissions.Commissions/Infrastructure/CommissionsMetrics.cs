using Prometheus;

namespace ClientCommissions.Commissions.Infrastructure;

public static class CommissionsMetrics
{
    public static readonly Counter EventsReceived =
        Metrics.CreateCounter("commissions_events_received_total", "Accepted commission events");

    public static readonly Counter EventsDuplicate =
        Metrics.CreateCounter("commissions_events_duplicate_total", "Duplicate events ignored on ingest");

    public static readonly Counter CommissionsCalculated =
        Metrics.CreateCounter("commissions_calculated_total", "Commission rows created by the calculator");

    public static readonly Counter PayoutsPublished =
        Metrics.CreateCounter("commissions_payouts_published_total", "Payout messages published to Kafka");
}
