using ClientCommissions.Commissions.Domain.Calculations;
using ClientCommissions.Commissions.Domain.Entities;
using FsCheck;
using Xunit;

namespace ClientCommissions.Commissions.Tests;

public sealed class CommissionEnginePropertyTests
{
    private static readonly CommissionEngine Engine = new();

    private static Gen<decimal> PositiveProfit() =>
        Gen.Choose(1, 1_000_000).Select(i => i / 100m);

    private static Gen<decimal> NonPositiveProfit() =>
        Gen.Choose(-1_000_000, 0).Select(i => i / 100m);

    private static Gen<IReadOnlyList<AncestorLink>> Chain() =>
        from n in Gen.Choose(0, 10)
        select (IReadOnlyList<AncestorLink>)Enumerable
            .Range(1, n)
            .Select(level => new AncestorLink(level, Guid.NewGuid()))
            .ToList();

    private static Gen<ICommissionScheme> Scheme() =>
        Gen.Elements<ICommissionScheme>(new LinearScheme(), new FibonacciScheme());

    private static Gen<(decimal profit, ICommissionScheme scheme, IReadOnlyList<AncestorLink> chain)> PositiveCase() =>
        from profit in PositiveProfit()
        from scheme in Scheme()
        from chain in Chain()
        select (profit, scheme, chain);

    [Fact]
    public void Positive_profit_produces_one_commission_per_chain_link()
    {
        Prop.ForAll(Arb.From(PositiveCase()), t =>
            Engine.Calculate(t.profit, t.scheme, t.chain).Count == t.chain.Count
        ).QuickCheckThrowOnFailure();
    }

    [Fact]
    public void Non_positive_profit_produces_nothing()
    {
        var gen =
            from profit in NonPositiveProfit()
            from scheme in Scheme()
            from chain in Chain()
            select (profit, scheme, chain);

        Prop.ForAll(Arb.From(gen), t =>
            Engine.Calculate(t.profit, t.scheme, t.chain).Count == 0
        ).QuickCheckThrowOnFailure();
    }

    [Fact]
    public void Every_commission_stamps_the_scheme_used()
    {
        Prop.ForAll(Arb.From(PositiveCase()), t =>
            Engine.Calculate(t.profit, t.scheme, t.chain).All(r => r.Schema == t.scheme.Type)
        ).QuickCheckThrowOnFailure();
    }

    [Fact]
    public void User_and_level_are_preserved_per_position()
    {
        Prop.ForAll(Arb.From(PositiveCase()), t =>
        {
            var result = Engine.Calculate(t.profit, t.scheme, t.chain);
            return result.Select(r => r.UserId).SequenceEqual(t.chain.Select(c => c.UserId))
                && result.Select(r => r.Level).SequenceEqual(t.chain.Select(c => c.Level));
        }).QuickCheckThrowOnFailure();
    }

    [Fact]
    public void All_amounts_are_non_negative()
    {
        Prop.ForAll(Arb.From(PositiveCase()), t =>
            Engine.Calculate(t.profit, t.scheme, t.chain).All(r => r.Amount >= 0m)
        ).QuickCheckThrowOnFailure();
    }

    [Fact]
    public void Amounts_are_non_decreasing_as_level_grows()
    {
        Prop.ForAll(Arb.From(PositiveCase()), t =>
        {
            var amounts = Engine.Calculate(t.profit, t.scheme, t.chain)
                .Select(r => r.Amount)
                .ToList();
            return amounts.SequenceEqual(amounts.OrderBy(a => a));
        }).QuickCheckThrowOnFailure();
    }

    [Fact]
    public void Calculation_is_deterministic()
    {
        Prop.ForAll(Arb.From(PositiveCase()), t =>
        {
            var first = Engine.Calculate(t.profit, t.scheme, t.chain);
            var second = Engine.Calculate(t.profit, t.scheme, t.chain);
            return first.SequenceEqual(second);
        }).QuickCheckThrowOnFailure();
    }
}
