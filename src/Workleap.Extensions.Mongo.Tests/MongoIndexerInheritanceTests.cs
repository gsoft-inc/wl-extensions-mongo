using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using Workleap.Extensions.Mongo;
using Workleap.Extensions.Mongo.Indexing;
using Workleap.Extensions.Xunit;

namespace Workleap.Extensions.Mongo.Tests;

public class MongoIndexerInheritanceTests : BaseIntegrationTest<MongoFixture>
{
    public MongoIndexerInheritanceTests(MongoFixture fixture, ITestOutputHelper testOutputHelper)
        : base(fixture, testOutputHelper)
    {
    }

    [Fact]
    public async Task UpdateIndexesAsync_Ignores_Automatically_Inherited_Index_Provider()
    {
        async Task AssertIndexes<TDocument>()
            where TDocument : IMongoDocument
        {
            using var indexCursor = await this.Services.GetRequiredService<IMongoCollection<TDocument>>().Indexes.ListAsync();
            var indexNames = await indexCursor.ToAsyncEnumerable().Select(x => x["name"].AsString).ToArrayAsync();

            Assert.Equal(2, indexNames.Length);
            Assert.Contains("_id_", indexNames);
            Assert.Contains("name_7a2f315f44b4b7813c1db6c66b1ae173", indexNames);
        }

        var documentTypes = new[] { typeof(BaseTestDocumentA) };
        await this.Services.GetRequiredService<IMongoIndexer>().UpdateIndexesAsync(documentTypes);

        await AssertIndexes<BaseTestDocumentA>();
        await AssertIndexes<ChildTestDocumentB>();
        await AssertIndexes<ChildTestDocumentC>();
    }

    [BsonKnownTypes(typeof(ChildTestDocumentB), typeof(ChildTestDocumentC))]
    [MongoCollection("inheritedIndexProviders", IndexProviderType = typeof(BaseTestDocumentAIndexProvider))]
    private class BaseTestDocumentA : MongoDocument
    {
        [BsonElement("name")]
        public string Name { get; set; } = string.Empty;
    }

    [BsonDiscriminator("B")]
    private sealed class ChildTestDocumentB : BaseTestDocumentA
    {
    }

    [BsonDiscriminator("C")]
    private sealed class ChildTestDocumentC : BaseTestDocumentA
    {
    }

    private sealed class BaseTestDocumentAIndexProvider : MongoIndexProvider<BaseTestDocumentA>
    {
        public override IEnumerable<CreateIndexModel<BaseTestDocumentA>> CreateIndexModels()
        {
            yield return new CreateIndexModel<BaseTestDocumentA>(
                Builders<BaseTestDocumentA>.IndexKeys.Ascending(x => x.Name),
                new CreateIndexOptions { Name = "name" });
        }
    }
}
