using Microsoft.Extensions.Options;

namespace Workleap.Extensions.Mongo.Ephemeral;

internal sealed class ConfigureEphemeralMongoClientOptions : IConfigureNamedOptions<MongoClientOptions>
{
    private readonly string _databaseName;
    private readonly ReusableMongoRunnerProvider _runnerProvider;

    public ConfigureEphemeralMongoClientOptions(DefaultDatabaseNameHolder defaultDatabaseNameHolder, ReusableMongoRunnerProvider runnerProvider)
    {
        this._databaseName = defaultDatabaseNameHolder.DatabaseName;
        this._runnerProvider = runnerProvider;
    }

    public void Configure(string? name, MongoClientOptions options)
    {
        // Each test that requests a IMongoDatabase will have its own database
        // There will also be one MongoDB instance per named MongoDB client
        var runner = this._runnerProvider.GetRunner(name ?? "");

        options.ConnectionString = runner.ConnectionString;
        options.DefaultDatabaseName = this._databaseName;
    }

    public void Configure(MongoClientOptions options)
    {
    }
}