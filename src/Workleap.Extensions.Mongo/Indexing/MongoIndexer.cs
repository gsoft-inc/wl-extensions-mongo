using System.Reflection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using Workleap.Extensions.Mongo.Threading;

namespace Workleap.Extensions.Mongo.Indexing;

internal sealed class MongoIndexer : IMongoIndexer
{
    private static readonly MethodInfo ProcessAsyncMethod = typeof(MongoIndexer).GetMethod(nameof(ProcessAsync), BindingFlags.NonPublic | BindingFlags.Instance)
        ?? throw new InvalidOperationException($"Could not find public instance method {nameof(MongoIndexer)}.{nameof(ProcessAsync)}");

    private readonly IMongoClientProvider _mongoClientProvider;
    private readonly IOptionsMonitor<MongoClientOptions> _optionsMonitor;
    private readonly ILoggerFactory _loggerFactory;

    public MongoIndexer(IMongoClientProvider mongoClientProvider,  IOptionsMonitor<MongoClientOptions> optionsMonitor, ILoggerFactory loggerFactory)
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

        var documentTypesWithExplicitMongoCollectionAttribute = assemblies.SelectMany(x => x.GetTypes())
            .Where(IsDocumentTypesWithExplicitMongoCollectionAttribute)
            .ToArray();

        return this.UpdateIndexesAsync(documentTypesWithExplicitMongoCollectionAttribute, clientName, databaseName, cancellationToken);
    }

    internal static bool IsDocumentTypesWithExplicitMongoCollectionAttribute(Type typeCandidate)
    {
        return typeCandidate.IsConcreteMongoDocumentType() && typeCandidate.GetCustomAttribute<MongoCollectionAttribute>(inherit: false) != null;
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
            if (!type.IsConcreteMongoDocumentType())
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

        var expectedIndexes = new Dictionary<string, IList<UniqueIndexName>>();

        foreach (var entry in registry)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var documentType = entry.DocumentType;
            var indexProviderType = entry.IndexProviderType;

            var indexProvider = Activator.CreateInstance(indexProviderType, Array.Empty<object>());
            if (indexProvider == null)
            {
                throw new Exception("An error occurred while instantiating type " + indexProviderType);
            }

            var processAsyncMethod = ProcessAsyncMethod.MakeGenericMethod(documentType);
            var task = (Task<IndexCreationResult>?)processAsyncMethod.Invoke(this, new[] { indexProvider, database, cancellationToken })
                ?? throw new InvalidOperationException($"'{nameof(MongoIndexer)}.{nameof(this.ProcessAsync)}(...)' should have returned a task");

            var processingResult = await task.ConfigureAwait(false);
            
            var collectionName = MongoReflectionCache.GetCollectionName(documentType);
            if (expectedIndexes.TryGetValue(collectionName, out var expectedIndexesForCollection))
            {
                // Better way to support AddRange?
                var concat = expectedIndexesForCollection.Concat(processingResult.ExpectedIndexes);
                expectedIndexes[collectionName] = concat.ToList();
            }
            else
            {
                expectedIndexes.Add(collectionName, processingResult.ExpectedIndexes);
            }
        }

        await IndexDeleter.ProcessAsync(database, expectedIndexes, this._loggerFactory, cancellationToken).ConfigureAwait(false);
    }

    private Task<IndexCreationResult> ProcessAsync<TDocument>(MongoIndexProvider<TDocument> provider, IMongoDatabase database, CancellationToken cancellationToken)
        where TDocument : class
    {
        return IndexCreator<TDocument>.ProcessAsync(provider, database, this._loggerFactory, cancellationToken);
    }
}