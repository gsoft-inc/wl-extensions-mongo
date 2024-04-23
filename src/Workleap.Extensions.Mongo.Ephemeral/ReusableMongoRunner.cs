using EphemeralMongo;

namespace Workleap.Extensions.Mongo.Ephemeral;

internal sealed class ReusableMongoRunner
{
    private readonly HashSet<Guid> _renters;
    private readonly object _lockObj;

    private IMongoRunner? _runner;
    private int _useCount;
    private string? _connectionString;

    public ReusableMongoRunner()
    {
        this._renters = new HashSet<Guid>();
        this._lockObj = new object();
    }

    public string ConnectionString
    {
        get => this._connectionString ?? throw new InvalidOperationException("Call " + nameof(ReusableMongoRunner) + "." + nameof(this.Rent) + "() before accessing the connection string");
    }

    public void Rent(Guid renter)
    {
        lock (this._lockObj)
        {
            if (this._renters.Contains(renter))
            {
                return;
            }

            // The lock and use count prevent multiple instances of local mongod processes for a same named MongoClient
            // that would degrade the overall performance.
            var options = new MongoRunnerOptions
            {
                UseSingleNodeReplicaSet = true,
                KillMongoProcessesWhenCurrentProcessExits = true,
            };

            var binaryDirectory = Environment.GetEnvironmentVariable("WORKLEAP_EXTENSIONS_MONGO_EPHEMERAL_BINARYDIRECTORY")?.Trim();
            if (!string.IsNullOrEmpty(binaryDirectory))
            {
                options.BinaryDirectory = binaryDirectory;
            }

            this._runner ??= MongoRunner.Run(options);
            this._useCount++;
            this._connectionString = this._runner.ConnectionString;
            this._renters.Add(renter);
        }
    }

    public void Return(Guid renter)
    {
        lock (this._lockObj)
        {
            if (!this._renters.Remove(renter))
            {
                return;
            }

            if (this._runner != null)
            {
                this._useCount--;
                if (this._useCount == 0)
                {
                    this._connectionString = null;
                    this._runner.Dispose();
                    this._runner = null;
                }
            }
        }
    }
}