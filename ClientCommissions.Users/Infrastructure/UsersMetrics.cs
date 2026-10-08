using Prometheus;

namespace ClientCommissions.Users.Infrastructure;

public static class UsersMetrics
{
    public static readonly Counter UsersCreated =
        Metrics.CreateCounter("users_created_total", "Users created");

    public static readonly Counter AncestorQueries =
        Metrics.CreateCounter("users_ancestor_queries_total", "Ancestor chain lookups served");
}
