using ClientCommissions.Commissions.Domain.Entities;

namespace ClientCommissions.Commissions.Domain.Calculations;

public sealed class FibonacciScheme : ICommissionScheme
{
    public SchemaType Type => SchemaType.Fibonacci;
    public decimal GetCoefficient(int level)
    {
        if (level < 1)
            throw new ArgumentOutOfRangeException(nameof(level), level, "Level must be >= 1");

        int prev = 1, curr = 1;
        for (var i = 3; i <= level; i++)
            (prev, curr) = (curr, prev + curr);

        return level == 1 ? prev : curr;
    }
}
