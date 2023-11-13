using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using Workleap.Extensions.Mongo.Indexing;

namespace Workleap.Extensions.Mongo.Tests;

public partial class MongoIndexerTests
{
    [Fact]
    public async Task UpdateIndexesAsync_Support_Multiple_Indexers_For_Same_Collection()
    {
        async Task AssertIndexes<TDocument>()
            where TDocument : IMongoDocument
        {
            using var indexCursor = await this.Services.GetRequiredService<IMongoCollection<TDocument>>().Indexes.ListAsync();
            var allIndexes = await indexCursor.ToAsyncEnumerable().ToListAsync();
            Assert.Equal(4, allIndexes.Count);
        }

        var documentTypes = new[] { typeof(BaseMultipleIndexersTestDocument), typeof(ChildMultipleIndexersTestDocument), typeof(SiblingMultipleIndexersTestDocument) };
        await this.Services.GetRequiredService<IMongoIndexer>().UpdateIndexesAsync(documentTypes);

        await AssertIndexes<BaseMultipleIndexersTestDocument>();
        await AssertIndexes<ChildMultipleIndexersTestDocument>();
        await AssertIndexes<SiblingMultipleIndexersTestDocument>();
    }
    
    [Fact]
    public async Task UpdateIndexesAsync_Correctly_Delete_Index_From_Multiple_Indexer_On_Same_Collection()
    {
        async Task AssertIndexes<TDocument>()
            where TDocument : IMongoDocument
        {
            using var indexCursor = await this.Services.GetRequiredService<IMongoCollection<TDocument>>().Indexes.ListAsync();
            var allIndexes = await indexCursor.ToAsyncEnumerable().ToListAsync();
            Assert.Equal(3, allIndexes.Count);
        }
    
        // Simulate a N-1 release where there is more than one indexer for the same collection
        var documentTypes = new[] { typeof(BaseMultipleIndexersTestDocument), typeof(ChildMultipleIndexersTestDocument), typeof(SiblingMultipleIndexersTestDocument) };
        await this.Services.GetRequiredService<IMongoIndexer>().UpdateIndexesAsync(documentTypes);
    
        // Simulate the N release where we remove some index and indexers
        var documentTypesPost = new[] { typeof(BaseMultipleIndexersTestDocument), typeof(ChildMultipleIndexersTestDocument2) };
        await this.Services.GetRequiredService<IMongoIndexer>().UpdateIndexesAsync(documentTypesPost);
        
        await AssertIndexes<BaseMultipleIndexersTestDocument>();
    }
    
    [BsonKnownTypes(typeof(ChildMultipleIndexersTestDocument))]
    [MongoCollection("multipleIndexProviders", IndexProviderType = typeof(BaseMultipleIndexersProvider))]
    private class BaseMultipleIndexersTestDocument : MongoDocument
    {
        [BsonElement("name")]
        public string Name { get; set; } = string.Empty;
    }

    [BsonDiscriminator("Children")]
    [MongoCollection("multipleIndexProviders", IndexProviderType = typeof(ChildMultipleIndexersProvider))]
    private sealed class ChildMultipleIndexersTestDocument : BaseMultipleIndexersTestDocument
    {
        [BsonElement("cad_addr")]
        public string CanadianAddresses { get; set; } = string.Empty;
        
        [BsonElement("us_addr")]
        public string UsAddresses { get; set; } = string.Empty;
    }
    
    [BsonDiscriminator("Children")]
    [MongoCollection("multipleIndexProviders", IndexProviderType = typeof(ChildMultipleIndexersProviderPost))]
    private sealed class ChildMultipleIndexersTestDocument2 : BaseMultipleIndexersTestDocument
    {
        [BsonElement("cad_addr")]
        public string CanadianAddresses { get; set; } = string.Empty;
        
        [BsonElement("us_addr")]
        public string UsAddresses { get; set; } = string.Empty;
    }
    
    [MongoCollection("multipleIndexProviders", IndexProviderType = typeof(SiblingMultipleIndexersProvider))]
    private sealed class SiblingMultipleIndexersTestDocument : MongoDocument 
    {
        [BsonElement("metadata")]
        public string Metadata { get; set; } = string.Empty;
    }
    
    // INDEXER
    private sealed class BaseMultipleIndexersProvider : MongoIndexProvider<BaseMultipleIndexersTestDocument>
    {
        public override IEnumerable<CreateIndexModel<BaseMultipleIndexersTestDocument>> CreateIndexModels()
        {
            yield return new CreateIndexModel<BaseMultipleIndexersTestDocument>(
                Builders<BaseMultipleIndexersTestDocument>.IndexKeys.Ascending(x => x.Name),
                new CreateIndexOptions { Name = "name" });
        }
    }
    
    private sealed class ChildMultipleIndexersProvider : MongoIndexProvider<ChildMultipleIndexersTestDocument>
    {
        public override IEnumerable<CreateIndexModel<ChildMultipleIndexersTestDocument>> CreateIndexModels()
        {
            yield return new CreateIndexModel<ChildMultipleIndexersTestDocument>(
                Builders<ChildMultipleIndexersTestDocument>
                    .IndexKeys.Ascending(x => x.CanadianAddresses),
                new CreateIndexOptions { Name = "addr" });
        }
    }
    
    private sealed class ChildMultipleIndexersProviderPost : MongoIndexProvider<ChildMultipleIndexersTestDocument2>
    {
        public override IEnumerable<CreateIndexModel<ChildMultipleIndexersTestDocument2>> CreateIndexModels()
        {
            yield return new CreateIndexModel<ChildMultipleIndexersTestDocument2>(
                Builders<ChildMultipleIndexersTestDocument2>
                    .IndexKeys.Ascending(x => x.UsAddresses),
                new CreateIndexOptions { Name = "addr" });
        }
    }
    
    private sealed class SiblingMultipleIndexersProvider : MongoIndexProvider<SiblingMultipleIndexersTestDocument>
    {
        public override IEnumerable<CreateIndexModel<SiblingMultipleIndexersTestDocument>> CreateIndexModels()
        {
            yield return new CreateIndexModel<SiblingMultipleIndexersTestDocument>(
                Builders<SiblingMultipleIndexersTestDocument>
                    .IndexKeys.Ascending(x => x.Metadata),
                new CreateIndexOptions { Name = "metadata" });
        }
    }
}
