using ClientCommissions.Commissions.Domain.Entities;

namespace ClientCommissions.Commissions.Domain.Calculations;

public sealed class CommissionSchemeResolver : ICommissionSchemeResolver
{
    private readonly Dictionary<SchemaType, ICommissionScheme> _schemes;

    public CommissionSchemeResolver(IEnumerable<ICommissionScheme> schemes)
        => _schemes = schemes.ToDictionary(s => s.Type);

    public ICommissionScheme Resolve(SchemaType type)
        => _schemes.TryGetValue(type, out var s)
            ? s
            : throw new ArgumentOutOfRangeException(nameof(type), $"Unknown schema: {type}");
}
