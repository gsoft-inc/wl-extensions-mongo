using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Workleap.Extensions.Mongo.Indexing;

/// <summary>
/// Delete indexes for a particular collection by comparing existing indexes in the database
/// with the expected indexes declared in the code.
/// </summary>
internal sealed class IndexDeleter
{
    private readonly IMongoDatabase _database;
    private readonly ILogger _logger;
    
    public IndexDeleter(IMongoDatabase database, ILogger<IndexDeleter> logger)
    {
        this._database = database;
        this._logger = logger;
    }
    
    public async Task ProcessAsync(Dictionary<string, IList<UniqueIndexName>> expectedIndexes, CancellationToken cancellationToken)
    {
        foreach (var expectedIndexForCollection in expectedIndexes)
        {
            var existingIndexesForCollection = await this.GetExistingIndexes(expectedIndexForCollection.Key).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            
            var indexesToRemove = ComputeIndexesToRemove(expectedIndexForCollection.Value, existingIndexesForCollection);
            cancellationToken.ThrowIfCancellationRequested();
            
            await this.RemoveIndexes(expectedIndexForCollection.Key, indexesToRemove).ConfigureAwait(false);
        }
    }
    
    private async Task<List<UniqueIndexName>> GetExistingIndexes(string collectionName)
    {
        var existingIndexes = new List<UniqueIndexName>();
        
        using var indexesCursor = await this._database.GetCollection<BsonDocument>(collectionName).Indexes.ListAsync().ConfigureAwait(false);
        var indexes = await indexesCursor.ToListAsync().ConfigureAwait(false);
        
        foreach (var index in indexes)
        {
            if (UniqueIndexName.TryCreate(index, out var indexName))
            {
                existingIndexes.Add(indexName);
            }
        }

        return existingIndexes;
    }

    private static Dictionary<UniqueIndexName, RemoveReason> ComputeIndexesToRemove(IList<UniqueIndexName> expectedIndexForCollection, List<UniqueIndexName> existingIndexesForCollection)
    {
        var indexesToDelete = new Dictionary<UniqueIndexName, RemoveReason>();
        foreach (var existingIndexes in existingIndexesForCollection)
        {
            var matchedExpectedIndexes = expectedIndexForCollection.FirstOrDefault(x => x.Prefix == existingIndexes.Prefix);
            if (matchedExpectedIndexes == null)
            {
                indexesToDelete.Add(existingIndexes, RemoveReason.Orphaned);
                continue;
            }

            if (matchedExpectedIndexes.Hash != existingIndexes.Hash)
            {
                indexesToDelete.Add(existingIndexes, RemoveReason.Outdated);
                continue;
            }
        }

        return indexesToDelete;
    }

    private async Task RemoveIndexes(string collectionName, Dictionary<UniqueIndexName, RemoveReason> indexesToRemove)
    {
        foreach (var indexToRemove in indexesToRemove)
        {
            var indexName = indexToRemove.Key;
            var reason = indexToRemove.Value;

            switch (reason)
            {
                case RemoveReason.Outdated:
                    this._logger.DroppingOutdatedIndex(collectionName, indexName.FullName);
                    break;
                case RemoveReason.Orphaned:
                    this._logger.DroppingOrphanedIndex(collectionName, indexName.FullName);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(reason));
            }
            
            await this._database.GetCollection<BsonDocument>(collectionName).Indexes.DropOneAsync(indexName.FullName).ConfigureAwait(false);
        }
    }

    [SuppressMessage("StyleCop.CSharp.OrderingRules", "SA1201:Elements should appear in the correct order", Justification = "These are private enums")]
    private enum RemoveReason
    {
        Outdated,
        Orphaned,
    }
}