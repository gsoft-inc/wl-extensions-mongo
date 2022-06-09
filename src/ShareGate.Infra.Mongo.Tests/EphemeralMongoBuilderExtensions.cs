using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Mongo2Go;

namespace ShareGate.Infra.Mongo.Tests;

public static class EphemeralMongoBuilderExtensions
{
    /// <summary>
    /// Provides a real implementation of MongoDB using a ephemeral localhost server that will be destroyed
    /// when it will no longer be used by any running test. The first startup can take approximately two seconds on a dev workstation.
    /// https://github.com/Mongo2Go/Mongo2Go
    /// </summary>
    public static MongoBuilder UseEphemeralRealServer(this MongoBuilder builder)
    {
        builder.Services.AddSingleton<PrivateMongoDbRunner>();
        builder.Services.ConfigureOptions<EphemeralMongoSetup>();

        return builder;
    }

    private sealed class PrivateMongoDbRunner : IDisposable
    {
        private static readonly object _lockObj = new object();
        private static MongoDbRunner? _runner;
        private static int _useCount;

        public PrivateMongoDbRunner()
        {
            lock (_lockObj)
            {
                // The lock and use count prevent multiple instances of local MongoDB that would degrade the overall performance
                _runner ??= MongoDbRunner.Start(singleNodeReplSet: true, logger: NullLogger.Instance);
                _useCount++;

                this.ConnectionString = _runner.ConnectionString;
            }
        }

        public string ConnectionString { get; }

        public void Dispose()
        {
            lock (_lockObj)
            {
                _useCount--;
                if (_useCount == 0 && _runner != null)
                {
                    _runner.Dispose();
                    _runner = null;
                }
            }
        }
    }

    private sealed class EphemeralMongoSetup : IConfigureOptions<MongoOptions>
    {
        private readonly PrivateMongoDbRunner _runner;

        public EphemeralMongoSetup(PrivateMongoDbRunner runner)
        {
            this._runner = runner;
        }

        public void Configure(MongoOptions options)
        {
            // Each test that requests a IMongoDatabase will have its own separate database and storage
            options.ConnectionString = this._runner.ConnectionString;
            options.DefaultDatabaseName = Guid.NewGuid().ToString("N");
        }
    }
}