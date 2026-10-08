using ClientCommissions.Commissions.Domain.Calculations;
using ClientCommissions.Commissions.Domain.Entities;
using Xunit;

namespace ClientCommissions.Commissions.Tests;

public class CommissionEngineTests
{
    private readonly CommissionEngine _engine = new();

    private static IReadOnlyList<AncestorLink> Chain(params int[] levels)
        => levels.Select(l => new AncestorLink(l, Guid.NewGuid())).ToList();

    [Theory]
    [InlineData(1, 10)]
    [InlineData(2, 20)]
    [InlineData(3, 30)]
    public void Linear_coefficient_equals_level(int level, decimal expected)
    {
        var r = _engine.Calculate(1000m, new LinearScheme(), Chain(level));
        Assert.Single(r);
        Assert.Equal(expected, r[0].Amount);
    }

    [Theory]
    [InlineData(1, 10)]
    [InlineData(2, 10)]
    [InlineData(3, 20)]
    [InlineData(4, 30)]
    [InlineData(10, 550)]
    public void Fibonacci_follows_sequence(int level, decimal expected)
    {
        var r = _engine.Calculate(1000m, new FibonacciScheme(), Chain(level));
        Assert.Single(r);
        Assert.Equal(expected, r[0].Amount);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-500)]
    public void NonPositive_profit_returns_empty(decimal profit)
    {
        var r = _engine.Calculate(profit, new LinearScheme(), Chain(1, 2, 3));
        Assert.Empty(r);
    }

    [Fact]
    public void Empty_chain_returns_empty()
    {
        var r = _engine.Calculate(1000m, new LinearScheme(), new List<AncestorLink>());
        Assert.Empty(r);
    }

    [Fact]
    public void Full_chain_computes_every_level_and_preserves_users()
    {
        var chain = Chain(1, 2, 3);
        var r = _engine.Calculate(1000m, new LinearScheme(), chain);

        Assert.Equal(3, r.Count);
        Assert.Equal(new[] { 10m, 20m, 30m }, r.Select(x => x.Amount));
        Assert.Equal(chain.Select(c => c.UserId), r.Select(x => x.UserId));
        Assert.Equal(chain.Select(c => c.Level), r.Select(x => x.Level));
    }

    [Fact]
    public void Stamps_the_schema_used()
    {
        var lin = _engine.Calculate(1000m, new LinearScheme(), Chain(1));
        var fib = _engine.Calculate(1000m, new FibonacciScheme(), Chain(1));
        Assert.Equal(SchemaType.Linear, lin[0].Schema);
        Assert.Equal(SchemaType.Fibonacci, fib[0].Schema);
    }

    [Theory]
    [InlineData(12.5, 0.12)]
    [InlineData(37.5, 0.38)]
    public void Uses_bankers_rounding_to_even(decimal profit, decimal expected)
    {
        var r = _engine.Calculate(profit, new LinearScheme(), Chain(1));
        Assert.Equal(expected, r[0].Amount);
    }
}
