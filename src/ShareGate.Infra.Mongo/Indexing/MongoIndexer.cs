using System.Reflection;
using ShareGate.Infra.Mongo.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace ShareGate.Infra.Mongo.Indexing;

internal sealed class MongoIndexer : IMongoIndexer
{
    private static readonly MethodInfo ProcessAsyncMethod = typeof(MongoIndexer).GetMethod(nameof(ProcessAsync), BindingFlags.NonPublic | BindingFlags.Instance)
        ?? throw new InvalidOperationException($"Could not find public instance method {nameof(MongoIndexer)}.{nameof(ProcessAsync)}");

    private readonly IMongoDatabase _database;
    private readonly MongoDistributedLockFactory _distributedLockFactory;
    private readonly IUniqueIndexNameFactory _indexNameFactory;
    private readonly IOptions<MongoOptions> _options;
    private readonly ILogger<MongoIndexer> _logger;

    public MongoIndexer(IMongoDatabase database, MongoDistributedLockFactory distributedLockFactory, IUniqueIndexNameFactory indexNameFactory, IOptions<MongoOptions> options, ILogger<MongoIndexer> logger)
    {
        this._database = database;
        this._distributedLockFactory = distributedLockFactory;
        this._indexNameFactory = indexNameFactory;
        this._options = options;
        this._logger = logger;
    }

    public Task UpdateIndexesAsync(IEnumerable<Assembly> assemblies, CancellationToken cancellationToken = default)
    {
        if (assemblies == null)
        {
            throw new ArgumentNullException(nameof(assemblies));
        }

        var types = assemblies.SelectMany(x => x.GetTypes()).Where(MongoReflectionCache.IsConcreteMongoDocumentType).ToArray();
        return this.UpdateIndexesAsync(types, cancellationToken);
    }

    public async Task UpdateIndexesAsync(IEnumerable<Type> types, CancellationToken cancellationToken = default)
    {
        if (types == null)
        {
            throw new ArgumentNullException(nameof(types));
        }

        var enumeratedTypes = new List<Type>();

        foreach (var type in types)
        {
            if (!MongoReflectionCache.IsConcreteMongoDocumentType(type))
            {
                throw new ArgumentException($"Type '{type}' must implement {nameof(IMongoDocument)}");
            }

            enumeratedTypes.Add(type);
        }

        var lockId = this._options.Value.Indexing.DistributedLockName;
        var lockLifetime = TimeSpan.FromSeconds(this._options.Value.Indexing.LockMaxLifetimeInSeconds);
        var acquireTimeout = TimeSpan.FromSeconds(this._options.Value.Indexing.LockAcquisitionTimeoutInSeconds);

        MongoDistributedLock distributedLock;
        await using (distributedLock = await this._distributedLockFactory.AcquireAsync(lockId, lockLifetime, acquireTimeout, cancellationToken).ConfigureAwait(false))
        {
            if (distributedLock.IsAcquired)
            {
                await this.UpdateIndexesInternalAsync(enumeratedTypes, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private async Task UpdateIndexesInternalAsync(IEnumerable<Type> types, CancellationToken cancellationToken = default)
    {
        var registry = new IndexRegistry(types);

        foreach (var kvp in registry)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var documentType = kvp.Key;
            var indexProviderType = kvp.Value;

            var indexProvider = Activator.CreateInstance(indexProviderType, Array.Empty<object>());
            if (indexProvider == null)
            {
                throw new Exception("An error occurred while instantiating type " + indexProviderType);
            }

            var processAsyncMethod = ProcessAsyncMethod.MakeGenericMethod(documentType);
            var task = (Task?)processAsyncMethod.Invoke(this, new[] { indexProvider, cancellationToken })
                ?? throw new InvalidOperationException($"'{nameof(MongoIndexer)}.{nameof(this.ProcessAsync)}(...)' should have returned a task");

            await task.ConfigureAwait(false);
        }
    }

    private Task ProcessAsync<TDocument>(MongoIndexProvider<TDocument> provider, CancellationToken cancellationToken)
        where TDocument : IMongoDocument
    {
        return IndexProcessor<TDocument>.ProcessAsync(provider, this._database, this._indexNameFactory, this._logger, cancellationToken);
    }
}