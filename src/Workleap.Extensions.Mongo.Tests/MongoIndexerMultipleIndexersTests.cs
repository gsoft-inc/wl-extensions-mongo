using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using Workleap.Extensions.Mongo.Indexing;
using Workleap.Extensions.Xunit;

namespace Workleap.Extensions.Mongo.Tests;

public class MongoIndexerMultipleIndexersTests : BaseIntegrationTest<MongoFixture>
{
    public MongoIndexerMultipleIndexersTests(MongoFixture fixture, ITestOutputHelper testOutputHelper)
        : base(fixture, testOutputHelper)
    {
    }
    
    [Fact]
    public async Task UpdateIndexesAsync_Support_Creating_Indexes_When_Multiple_Indexers_For_Same_Collection()
    {
        async Task AssertIndexes<TDocument>()
            where TDocument : IMongoDocument
        {
            using var indexCursor = await this.Services.GetRequiredService<IMongoCollection<TDocument>>().Indexes.ListAsync();
            var indexNames = await indexCursor.ToAsyncEnumerable().Select(x => x["name"].AsString).ToArrayAsync();
            
            Assert.Equal(4, indexNames.Length);
            Assert.Contains("_id_", indexNames);
            Assert.Contains("name_7a2f315f44b4b7813c1db6c66b1ae173", indexNames);
            Assert.Contains("contact_2ec2ef6cef9b5c4182df182eb4e1e857", indexNames);
            Assert.Contains("metadata_fbf67839566769a7660548cfcb674468", indexNames);
        }

        var documentTypes = new[] { typeof(BaseMultipleIndexersTestDocument), typeof(ChildMultipleIndexersTestDocument), typeof(SiblingMultipleIndexersTestDocument) };
        await this.Services.GetRequiredService<IMongoIndexer>().UpdateIndexesAsync(documentTypes);

        await AssertIndexes<BaseMultipleIndexersTestDocument>();
        await AssertIndexes<ChildMultipleIndexersTestDocument>();
        await AssertIndexes<SiblingMultipleIndexersTestDocument>();
    }
    
    [Fact]
    public async Task UpdateIndexesAsync_Support_Deleting_Indexes_When_Multiple_Indexers_For_Same_Collection()
    {
        async Task AssertIndexes<TDocument>()
            where TDocument : IMongoDocument
        {
            using var indexCursor = await this.Services.GetRequiredService<IMongoCollection<TDocument>>().Indexes.ListAsync();
            var indexNames = await indexCursor.ToAsyncEnumerable().Select(x => x["name"].AsString).ToArrayAsync();

            Assert.Equal(3, indexNames.Length);
            Assert.Contains("_id_", indexNames);
            Assert.Contains("name_7a2f315f44b4b7813c1db6c66b1ae173", indexNames);
            Assert.Contains("contact_800a8c19f5008939a89b582cc7c22e40", indexNames);
        }
    
        // Simulate a N-1 release with some initial indexes
        var documentTypes = new[] { typeof(BaseMultipleIndexersTestDocument), typeof(ChildMultipleIndexersTestDocument), typeof(SiblingMultipleIndexersTestDocument) };
        await this.Services.GetRequiredService<IMongoIndexer>().UpdateIndexesAsync(documentTypes);
    
        // Simulate the N release where we remove one indexer and update an other 
        var updatedDocumentTypes = new[] { typeof(BaseMultipleIndexersTestDocument), typeof(UpdatedChildMultipleIndexersTestDocument) };
        await this.Services.GetRequiredService<IMongoIndexer>().UpdateIndexesAsync(updatedDocumentTypes);
        
        await AssertIndexes<BaseMultipleIndexersTestDocument>();
    }
    
    [BsonKnownTypes(typeof(ChildMultipleIndexersTestDocument))]
    [MongoCollection("multipleIndexProviders", IndexProviderType = typeof(BaseMultipleIndexersProvider))]
    private class BaseMultipleIndexersTestDocument : MongoDocument
    {
        [BsonElement("name")]
        public string Name { get; set; } = string.Empty;
    }
    
    private sealed class BaseMultipleIndexersProvider : MongoIndexProvider<BaseMultipleIndexersTestDocument>
    {
        public override IEnumerable<CreateIndexModel<BaseMultipleIndexersTestDocument>> CreateIndexModels()
        {
            yield return new CreateIndexModel<BaseMultipleIndexersTestDocument>(
                Builders<BaseMultipleIndexersTestDocument>.IndexKeys.Ascending(x => x.Name),
                new CreateIndexOptions { Name = "name" });
        }
    }

    [BsonDiscriminator("Children")]
    [MongoCollection("multipleIndexProviders", IndexProviderType = typeof(ChildMultipleIndexersProvider))]
    private class ChildMultipleIndexersTestDocument : BaseMultipleIndexersTestDocument
    {
        [BsonElement("email")]
        public string Email { get; set; } = string.Empty;
        
        [BsonElement("phone")]
        public string PhoneNumber { get; set; } = string.Empty;
    }
    
    private sealed class ChildMultipleIndexersProvider : MongoIndexProvider<ChildMultipleIndexersTestDocument>
    {
        public override IEnumerable<CreateIndexModel<ChildMultipleIndexersTestDocument>> CreateIndexModels()
        {
            yield return new CreateIndexModel<ChildMultipleIndexersTestDocument>(
                Builders<ChildMultipleIndexersTestDocument>
                    .IndexKeys.Ascending(x => x.Email),
                new CreateIndexOptions { Name = "contact" });
        }
    }
    
    // Represent an update in the indexes.
    [BsonDiscriminator("Children")]
    [MongoCollection("multipleIndexProviders", IndexProviderType = typeof(UpdatedChildMultipleIndexersProvider))]
    private sealed class UpdatedChildMultipleIndexersTestDocument : ChildMultipleIndexersTestDocument
    {
    }
    
    private sealed class UpdatedChildMultipleIndexersProvider : MongoIndexProvider<UpdatedChildMultipleIndexersTestDocument>
    {
        public override IEnumerable<CreateIndexModel<UpdatedChildMultipleIndexersTestDocument>> CreateIndexModels()
        {
            yield return new CreateIndexModel<UpdatedChildMultipleIndexersTestDocument>(
                Builders<UpdatedChildMultipleIndexersTestDocument>
                    .IndexKeys.Ascending(x => x.PhoneNumber),
                new CreateIndexOptions { Name = "contact" });
        }
    }
    
    [MongoCollection("multipleIndexProviders", IndexProviderType = typeof(SiblingMultipleIndexersProvider))]
    private sealed class SiblingMultipleIndexersTestDocument : MongoDocument 
    {
        [BsonElement("metadata")]
        public string Metadata { get; set; } = string.Empty;
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
