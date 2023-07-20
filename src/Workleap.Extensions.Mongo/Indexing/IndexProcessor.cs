using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Workleap.Extensions.Mongo.Indexing;

/// <summary>
/// Create, update or delete indexes for a particular document type by comparing existing indexes in the database
/// with the desired indexes definitions declared in the code.
/// </summary>
/// <remarks>
/// If an index takes a lot of resources to be created to the point where this could impact our SLOs, another solution would be
/// to create the index in another process (maybe the CI) on a secondary node, than switch the secondary node as primary and let the replication do its job:
/// https://www.mongodb.com/docs/manual/tutorial/force-member-to-be-primary/
/// </remarks>
internal sealed class IndexProcessor<TDocument>
    where TDocument : IMongoDocument
{
    private readonly MongoIndexProvider<TDocument> _provider;
    private readonly IMongoDatabase _database;
    private readonly ILogger _logger;
    private readonly CancellationToken _cancellationToken;
    private readonly string _collectionName;

    private readonly HashSet<UniqueIndexName> _existingIndexes;
    private readonly Dictionary<UniqueIndexName, CreateIndexModel<TDocument>> _indexModels;
    private readonly Dictionary<UniqueIndexName, RemoveReason> _indexesToRemove;
    private readonly Dictionary<UniqueIndexName, AddReason> _indexesToAdd;

    private IndexProcessor(MongoIndexProvider<TDocument> provider, IMongoDatabase database, ILoggerFactory loggerFactory, CancellationToken cancellationToken)
    {
        this._provider = provider;
        this._database = database;
        this._logger = loggerFactory.CreateLogger<IndexProcessor<TDocument>>();
        this._cancellationToken = cancellationToken;
        this._collectionName = database.GetCollectionName<TDocument>();

        this._existingIndexes = new HashSet<UniqueIndexName>();
        this._indexModels = new Dictionary<UniqueIndexName, CreateIndexModel<TDocument>>();
        this._indexesToRemove = new Dictionary<UniqueIndexName, RemoveReason>();
        this._indexesToAdd = new Dictionary<UniqueIndexName, AddReason>();
    }

    public static Task ProcessAsync(MongoIndexProvider<TDocument> provider, IMongoDatabase database, ILoggerFactory loggerFactory, CancellationToken cancellationToken)
    {
        return new IndexProcessor<TDocument>(provider, database, loggerFactory, cancellationToken).ProcessAsync();
    }

    private async Task ProcessAsync()
    {
        await this.EnsureCollectionExists().ConfigureAwait(false);
        this._cancellationToken.ThrowIfCancellationRequested();

        await this.PopulateExistingIndexes().ConfigureAwait(false);
        this._cancellationToken.ThrowIfCancellationRequested();

        this.PopulateIndexDescriptors();
        this._cancellationToken.ThrowIfCancellationRequested();

        this.ComputeIndexesToAddAndRemove();
        this._cancellationToken.ThrowIfCancellationRequested();

        // Index removal and creation should not be interrupted
        await this.RemoveIndexes().ConfigureAwait(false);
        await this.AddIndexes().ConfigureAwait(false);
    }

    private async Task EnsureCollectionExists()
    {
        var filteringOptions = new ListCollectionNamesOptions
        {
            Filter = Builders<BsonDocument>.Filter.Eq("name", this._collectionName),
        };

        using var collectionNamesCursor = await this._database.ListCollectionNamesAsync(filteringOptions).ConfigureAwait(false);
        var collectionAlreadyExists = await collectionNamesCursor.AnyAsync().ConfigureAwait(false);

        if (!collectionAlreadyExists)
        {
            await this._database.CreateCollectionAsync(this._collectionName).ConfigureAwait(false);
        }
    }

    private async Task PopulateExistingIndexes()
    {
        using var indexesCursor = await this._database.GetCollection<TDocument>().Indexes.ListAsync().ConfigureAwait(false);
        var indexes = await indexesCursor.ToListAsync().ConfigureAwait(false);

        foreach (var index in indexes)
        {
            if (UniqueIndexName.TryCreate(index, out var indexName))
            {
                this._existingIndexes.Add(indexName);
            }
        }
    }

    private void PopulateIndexDescriptors()
    {
        foreach (var indexModel in this._provider.CreateIndexModels())
        {
            if (!UniqueIndexName.TryCreate(indexModel, out var indexName))
            {
                throw new ArgumentException($"All indexes in '{this._provider.GetType()}' must provide a non-empty snake-cased name");
            }

            this._indexModels[indexName] = indexModel;
        }
    }

    private void ComputeIndexesToAddAndRemove()
    {
        foreach (var newIndexName in this._indexModels.Keys)
        {
            var existingIndexName = this._existingIndexes.FirstOrDefault(x => x.Prefix == newIndexName.Prefix);
            if (existingIndexName == null)
            {
                this._indexesToAdd.Add(newIndexName, AddReason.New);
                continue;
            }

            if (existingIndexName.Hash == newIndexName.Hash)
            {
                this._logger.SkippingUpToDateIndex(typeof(TDocument).Name, existingIndexName.FullName);
            }
            else
            {
                this._indexesToRemove.Add(existingIndexName, RemoveReason.Outdated);
                this._indexesToAdd.Add(newIndexName, AddReason.Updated);
            }

            this._existingIndexes.Remove(existingIndexName);
        }

        foreach (var existingIndex in this._existingIndexes)
        {
            this._indexesToRemove.Add(existingIndex, RemoveReason.Orphaned);
        }
    }

    private async Task RemoveIndexes()
    {
        foreach (var kvp in this._indexesToRemove)
        {
            var indexName = kvp.Key;
            var reason = kvp.Value;

            switch (reason)
            {
                case RemoveReason.Outdated:
                    this._logger.DroppingOutdatedIndex(typeof(TDocument).Name, indexName.FullName);
                    break;
                case RemoveReason.Orphaned:
                    this._logger.DroppingOrphanedIndex(typeof(TDocument).Name, indexName.FullName);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(reason));
            }

            await this._database.GetCollection<TDocument>().Indexes.DropOneAsync(indexName.FullName).ConfigureAwait(false);
        }
    }

    private async Task AddIndexes()
    {
        foreach (var kvp in this._indexesToAdd)
        {
            var indexName = kvp.Key;
            var reason = kvp.Value;

            var indexModel = this._indexModels[indexName];
            var clonedIndexModel = new CreateIndexModel<TDocument>(indexModel.Keys, indexModel.Options);
            clonedIndexModel.Options.Name = indexName.FullName;

            switch (reason)
            {
                case AddReason.New:
                    this._logger.CreatingCompletelyNewIndex(typeof(TDocument).Name, indexName.FullName);
                    break;
                case AddReason.Updated:
                    this._logger.CreatingUpdatedIndex(typeof(TDocument).Name, indexName.FullName);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(reason));
            }

            await this._database.GetCollection<TDocument>().Indexes.CreateOneAsync(clonedIndexModel).ConfigureAwait(false);
        }
    }

    [SuppressMessage("StyleCop.CSharp.OrderingRules", "SA1201:Elements should appear in the correct order", Justification = "These are private enums")]
    private enum AddReason
    {
        New,
        Updated,
    }

    [SuppressMessage("StyleCop.CSharp.OrderingRules", "SA1201:Elements should appear in the correct order", Justification = "These are private enums")]
    private enum RemoveReason
    {
        Outdated,
        Orphaned,
    }
}