namespace ShareGate.Infra.Mongo.Ephemeral;

internal sealed class DefaultDatabaseNameHolder
{
    public DefaultDatabaseNameHolder()
    {
        // Each test gets its own database
        this.DatabaseName = Guid.NewGuid().ToString("N");
    }

    public string DatabaseName { get; }
}