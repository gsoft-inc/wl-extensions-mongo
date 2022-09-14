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

    private readonly IMongoClientProvider _mongoClientProvider;
    private readonly IOptionsMonitor<MongoClientOptions> _optionsMonitor;
    private readonly ILoggerFactory _loggerFactory;

    public MongoIndexer(IMongoClientProvider mongoClientProvider, IOptionsMonitor<MongoClientOptions> optionsMonitor, ILoggerFactory loggerFactory)
    {
        this._mongoClientProvider = mongoClientProvider;
        this._optionsMonitor = optionsMonitor;
        this._loggerFactory = loggerFactory;
    }

    public Task UpdateIndexesAsync(Assembly assembly, string? clientName = null, string? databaseName = null, CancellationToken cancellationToken = default)
    {
        return this.UpdateIndexesAsync(new[] { assembly }, clientName, databaseName, cancellationToken);
    }

    public Task UpdateIndexesAsync(IEnumerable<Assembly> assemblies, string? clientName = null, string? databaseName = null, CancellationToken cancellationToken = default)
    {
        if (assemblies == null)
        {
            throw new ArgumentNullException(nameof(assemblies));
        }

        var types = assemblies.SelectMany(x => x.GetTypes()).Where(MongoReflectionCache.IsConcreteMongoDocumentType).ToArray();
        return this.UpdateIndexesAsync(types, clientName, databaseName, cancellationToken);
    }

    public async Task UpdateIndexesAsync(IEnumerable<Type> types, string? clientName = null, string? databaseName = null, CancellationToken cancellationToken = default)
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

        // Use default MongoDB client by default
        clientName ??= MongoDefaults.ClientName;
        var mongoClient = this._mongoClientProvider.GetClient(clientName);
        var options = this._optionsMonitor.Get(clientName);

        // Use default MongoDB database by default
        databaseName ??= options.DefaultDatabaseName;
        var database = mongoClient.GetDatabase(databaseName);

        // Attempt to update the indexes if we acquire the distributed lock
        var lockId = options.Indexing.DistributedLockName;
        var lockLifetime = TimeSpan.FromSeconds(options.Indexing.LockMaxLifetimeInSeconds);
        var acquireTimeout = TimeSpan.FromSeconds(options.Indexing.LockAcquisitionTimeoutInSeconds);
        var distributedLockFactory = new MongoDistributedLockFactory(database, this._loggerFactory);

        MongoDistributedLock distributedLock;
        await using (distributedLock = await distributedLockFactory.AcquireAsync(lockId, lockLifetime, acquireTimeout, cancellationToken).ConfigureAwait(false))
        {
            if (distributedLock.IsAcquired)
            {
                await this.UpdateIndexesInternalAsync(enumeratedTypes, database, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private async Task UpdateIndexesInternalAsync(IEnumerable<Type> types, IMongoDatabase database, CancellationToken cancellationToken = default)
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
            var task = (Task?)processAsyncMethod.Invoke(this, new[] { indexProvider, database, cancellationToken })
                ?? throw new InvalidOperationException($"'{nameof(MongoIndexer)}.{nameof(this.ProcessAsync)}(...)' should have returned a task");

            await task.ConfigureAwait(false);
        }
    }

    private Task ProcessAsync<TDocument>(MongoIndexProvider<TDocument> provider, IMongoDatabase database, CancellationToken cancellationToken)
        where TDocument : IMongoDocument
    {
        return IndexProcessor<TDocument>.ProcessAsync(provider, database, this._loggerFactory, cancellationToken);
    }
}