using Microsoft.Extensions.Logging;
using MongoDB.Driver;

namespace Workleap.Extensions.Mongo.Threading;

internal sealed class MongoDistributedLockFactory
{
    private readonly IMongoCollection<DistributedLockDocument> _collection;
    private readonly ILogger<MongoDistributedLock> _logger;

    public MongoDistributedLockFactory(IMongoDatabase database, ILoggerFactory loggerFactory)
    {
        this._collection = database.GetCollection<DistributedLockDocument>();
        this._logger = loggerFactory.CreateLogger<MongoDistributedLock>();
    }

    public ValueTask<MongoDistributedLock> AcquireAsync(string lockId, int lifetime, int timeout, CancellationToken cancellationToken = default)
    {
        return this.AcquireAsync(lockId, TimeSpan.FromMilliseconds(lifetime), TimeSpan.FromMilliseconds(timeout), cancellationToken);
    }

    public async ValueTask<MongoDistributedLock> AcquireAsync(string lockId, TimeSpan lifetime, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        if (lockId == null)
        {
            throw new ArgumentNullException(nameof(lockId));
        }

        if (lockId.Length == 0)
        {
            throw new ArgumentException("Lock ID cannot be empty", nameof(lockId));
        }

        if (lifetime <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(lifetime), "Lock lifetime must be greater than zero");
        }

        if (timeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout), "Lock acquisition timeout must be greater than zero");
        }

        var distributedLock = new MongoDistributedLock(this._collection, this._logger, lockId);
        await distributedLock.AcquireAsync(lifetime, timeout, cancellationToken).ConfigureAwait(false);
        return distributedLock;
    }
}