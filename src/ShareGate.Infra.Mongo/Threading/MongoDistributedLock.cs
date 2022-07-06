using Microsoft.Extensions.Logging;
using MongoDB.Driver;

namespace ShareGate.Infra.Mongo.Threading;

internal sealed class MongoDistributedLock
{
    private readonly IMongoCollection<DistributedLockDocument> _collection;
    private readonly ILogger<MongoDistributedLock> _logger;
    private readonly string _lockId;
    private readonly string _ownerId;
    private bool _isDisposed;

    public MongoDistributedLock(IMongoCollection<DistributedLockDocument> collection, ILogger<MongoDistributedLock> logger, string lockId)
    {
        this._collection = collection;
        this._logger = logger;
        this._lockId = lockId;
        this._ownerId = Guid.NewGuid().ToString("D");
    }

    public bool IsAcquired { get; private set; }

    public async ValueTask AcquireAsync(TimeSpan lifetime, TimeSpan timeout, CancellationToken cancellationToken)
    {
        this._logger.AcquiringDistributedLock(this._lockId, this._ownerId);

        this.IsAcquired = await this.AcquireAsyncInternal(lifetime, timeout, cancellationToken).ConfigureAwait(false);

        if (this.IsAcquired)
        {
            this._logger.DistributedLockAcquired(this._lockId, this._ownerId);
        }
        else
        {
            this._logger.DistributedLockNotAcquired(this._lockId, this._ownerId);
        }
    }

    private async ValueTask<bool> AcquireAsyncInternal(TimeSpan lifetime, TimeSpan timeout, CancellationToken cancellationToken)
    {
        using var timeoutCts = new CancellationTokenSource(timeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
        var linkedCt = linkedCts.Token;

        try
        {
            while (true)
            {
                if (await this.TryAcquireAsync(lifetime).ConfigureAwait(false))
                {
                    return true;
                }

                await this.WaitForLockToBeAvailableAsync(linkedCt).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            if (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
                return false;
            }

            throw;
        }
    }

    private async ValueTask<bool> TryAcquireAsync(TimeSpan lifetime)
    {
        var filter = new ExpressionFilterDefinition<DistributedLockDocument>(x => x.Id == this._lockId && (!x.IsAcquired || x.ExpiresAt < DateTime.UtcNow.Ticks));

        // Any acquired lock becomes automatically available once its expiration date is reached
        var update = Builders<DistributedLockDocument>.Update
            .Set(x => x.IsAcquired, true)
            .Set(x => x.ExpiresAt, DateTime.UtcNow.Ticks + lifetime.Ticks)
            .Set(x => x.OwnerId, this._ownerId);

        // Upsert populates the _id field from the filter, no need to specify update.SetOnInsert(...)
        var options = new UpdateOptions { IsUpsert = true };

        try
        {
            // MongoDB write operations are atomic. If multiple threads try to acquire a lock with the same ID, only one will succeed.
            // The other threads will have no result because of the filter.
            // See: https://www.mongodb.com/docs/manual/core/write-operations-atomicity/#atomicity
            var updateResult = await this._collection.UpdateOneAsync(filter, update, options).ConfigureAwait(false);
            return updateResult.IsAcknowledged;
        }
        catch (MongoWriteException ex) when (ex.WriteError.Category == ServerErrorCategory.DuplicateKey)
        {
            // This is expected when another instance already owns the lock. As the filter returns no result, the upsert operation
            // becomes an insert operation which fails because the _id is already used.
            return false;
        }
    }

    private async ValueTask WaitForLockToBeAvailableAsync(CancellationToken cancellationToken)
    {
        // "Watch" the lock collection until we get a signal that the desired lock becomes available
        // Watch uses change streams that require a replica set - even with a single node
        // https://www.mongodb.com/docs/manual/changeStreams/
        var watchPipeline = new EmptyPipelineDefinition<ChangeStreamDocument<DistributedLockDocument>>()
            .Match(x => x.OperationType == ChangeStreamOperationType.Update && x.FullDocument.Id == this._lockId && (!x.FullDocument.IsAcquired || x.FullDocument.ExpiresAt < DateTime.UtcNow.Ticks));

        var watchOptions = new ChangeStreamOptions
        {
            // Required in order for our filter to access the full document
            FullDocument = ChangeStreamFullDocumentOption.UpdateLookup,

            // Delay between each "getMore" command, default is one second
            MaxAwaitTime = TimeSpan.FromSeconds(10),
        };

        while (true)
        {
            using var watchCursor = await this._collection.WatchAsync(watchPipeline, watchOptions, cancellationToken).ConfigureAwait(false);

            while (await watchCursor.MoveNextAsync(cancellationToken).ConfigureAwait(false))
            {
                var isLockAvailable = watchCursor.Current.Any();
                if (isLockAvailable)
                {
                    return;
                }
            }
        }
    }

    private async ValueTask ReleaseAsync()
    {
        if (this.IsAcquired)
        {
            var filter = new ExpressionFilterDefinition<DistributedLockDocument>(x => x.Id == this._lockId && x.OwnerId == this._ownerId);
            var update = Builders<DistributedLockDocument>.Update.Set(x => x.IsAcquired, false).Set(x => x.OwnerId, string.Empty);

            // Updating an existing document instead of deleting it seems to keep the write atomicity we need in the TryAcquireAsync method
            // We don't mind having a few unused lock documents in the database
            await this._collection.UpdateOneAsync(filter, update).ConfigureAwait(false);
            this._logger.DistributedLockReleased(this._lockId, this._ownerId);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (!this._isDisposed)
        {
            await this.ReleaseAsync().ConfigureAwait(false);
            this._isDisposed = true;
        }
    }
}