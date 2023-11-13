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
internal sealed class IndexDeleter
{
    private readonly IMongoDatabase _database;
    private readonly ILogger _logger;
    private readonly CancellationToken _cancellationToken = CancellationToken.None;
    
    public IndexDeleter(IMongoDatabase database, ILogger<IndexDeleter> logger)
    {
        this._database = database;
        this._logger = logger;
    }
    
    public async Task DeleteAsync(Dictionary<string, IList<UniqueIndexName>> expectedIndexes)
    {
        foreach (var expectedIndexForCollection in expectedIndexes)
        {
            var existingIndexesForCollection = await this.GetExistingIndexes(expectedIndexForCollection.Key).ConfigureAwait(false);
            this._cancellationToken.ThrowIfCancellationRequested();
            
            var indexesToRemove = ComputeIndexesToRemove(expectedIndexForCollection.Value, existingIndexesForCollection);
            this._cancellationToken.ThrowIfCancellationRequested();
            
            await this.RemoveIndexes(expectedIndexForCollection.Key, indexesToRemove).ConfigureAwait(false);
        }
    }
    
    private async Task<List<UniqueIndexName>> GetExistingIndexes(string collectionName)
    {
        var existingIndexes = new List<UniqueIndexName>();
        
        // TODO: Think of better GetCollection<Object>  
        using var indexesCursor = await this._database.GetCollection<DocumentPlaceholder>(collectionName).Indexes.ListAsync().ConfigureAwait(false);
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

    private static List<UniqueIndexName> ComputeIndexesToRemove(IList<UniqueIndexName> expectedIndexForCollection, List<UniqueIndexName> existingIndexesForCollection)
    {
        var indexesToDelete = new List<UniqueIndexName>();
        foreach (var existingIndexes in existingIndexesForCollection)
        {
            var matchedExpectedIndexes = expectedIndexForCollection.FirstOrDefault(x => x.Prefix == existingIndexes.Prefix);
            if (matchedExpectedIndexes == null)
            {
                // Log: do orphaned
                indexesToDelete.Add(existingIndexes);
                continue;
            }

            if (matchedExpectedIndexes.Hash != existingIndexes.Hash)
            {
                // Log: outdated
                indexesToDelete.Add(existingIndexes);
                continue;
            }
        }

        return indexesToDelete;
    }

    private async Task RemoveIndexes(string collectionName, List<UniqueIndexName> indexesToRemove)
    {
        foreach (var indexToRemove in indexesToRemove)
        {
            var indexName = indexToRemove;
            var reason = RemoveReason.Outdated; // TODO: really forward the reason

            // switch (reason)
            // {
            //     case RemoveReason.Outdated:
            //         // this._logger.DroppingOutdatedIndex(typeof(TDocument).Name, indexName.FullName);
            //         break;
            //     case RemoveReason.Orphaned:
            //         // this._logger.DroppingOrphanedIndex(typeof(TDocument).Name, indexName.FullName);
            //         break;
            //     default:
            //         throw new ArgumentOutOfRangeException(nameof(reason));
            // }
            await this._database.GetCollection<DocumentPlaceholder>(collectionName).Indexes.DropOneAsync(indexName.FullName).ConfigureAwait(false);
        }
    }

    public class DocumentPlaceholder
    {
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