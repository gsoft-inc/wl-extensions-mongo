using EphemeralMongo;

namespace ShareGate.Infra.Mongo.Ephemeral;

internal sealed class ReusableMongoDbRunner : IDisposable
{
    private static readonly object _lockObj = new object();
    private static IMongoRunner? _runner;
    private static int _useCount;

    public ReusableMongoDbRunner()
    {
        lock (_lockObj)
        {
            // The lock and use count prevent multiple instances of local mongod processes that would degrade the overall performance
            _runner ??= MongoRunner.Run(new MongoRunnerOptions
            {
                UseSingleNodeReplicaSet = true,
            });

            _useCount++;

            this.ConnectionString = _runner.ConnectionString;
        }
    }

    public string ConnectionString { get; }

    public void Dispose()
    {
        lock (_lockObj)
        {
            if (_runner != null)
            {
                _useCount--;
                if (_useCount == 0)
                {
                    _runner.Dispose();
                    _runner = null;
                }
            }
        }
    }
}