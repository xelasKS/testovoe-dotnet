using ClientCommissions.Commissions.Domain.Entities;
using ClientCommissions.Commissions.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace ClientCommissions.Commissions.Application;

public sealed class SchemaService : ISchemaService
{
    private readonly ApplicationContext _db;
    private readonly ILogger<SchemaService> _logger;

    public SchemaService(ApplicationContext db, ILogger<SchemaService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<SchemaType> GetCurrentAsync(CancellationToken ct) =>
        (await _db.SchemaSettings.FirstAsync(ct)).Schema;

    public async Task SwitchAsync(SchemaType schema, CancellationToken ct)
    {
        var setting = await _db.SchemaSettings.FirstAsync(ct);
        setting.Change(schema);
        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Active commission schema switched to {Schema}", schema);
    }
}
