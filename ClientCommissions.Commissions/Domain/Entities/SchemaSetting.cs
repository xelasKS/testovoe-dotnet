namespace ClientCommissions.Commissions.Domain.Entities;

public class SchemaSetting
{
    public int Id { get; private set; }
    public SchemaType Schema { get; private set; }

    private SchemaSetting() { }

    public SchemaSetting(SchemaType schema)
    {
        Id = 1;
        Schema = schema;
    }

    public void Change(SchemaType schema) => Schema = schema;
}
