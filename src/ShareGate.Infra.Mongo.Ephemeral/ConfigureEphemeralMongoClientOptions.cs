using Microsoft.Extensions.Options;

namespace ShareGate.Infra.Mongo.Ephemeral;

internal sealed class ConfigureEphemeralMongoClientOptions : IConfigureNamedOptions<MongoClientOptions>
{
    private readonly string _databaseName;
    private readonly ReusableMongoDbRunner _runner;

    public ConfigureEphemeralMongoClientOptions(DefaultDatabaseNameHolder defaultDatabaseNameHolder, ReusableMongoDbRunner runner)
    {
        this._databaseName = defaultDatabaseNameHolder.DatabaseName;
        this._runner = runner;
    }

    public void Configure(string name, MongoClientOptions options)
    {
        this.Configure(options);
    }

    public void Configure(MongoClientOptions options)
    {
        // Each test that requests a IMongoDatabase will have its own database
        options.ConnectionString = this._runner.ConnectionString;
        options.DefaultDatabaseName = this._databaseName;
    }
}