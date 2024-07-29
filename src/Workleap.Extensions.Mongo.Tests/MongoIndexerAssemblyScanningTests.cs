using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using Workleap.Extensions.Mongo.Indexing;

namespace Workleap.Extensions.Mongo.Tests;

public class MongoIndexerAssemblyScanningTests
{
    [Fact]
    public async Task IsDocumentTypesWithExplicitMongoCollectionAttribute_Filter_Works()
    {
        Assert.False(MongoIndexer.IsDocumentTypesWithExplicitMongoCollectionAttribute(typeof(RandomObject)));
        Assert.True(MongoIndexer.IsDocumentTypesWithExplicitMongoCollectionAttribute(typeof(BaseAssemblyScanningTestDocument)));
        Assert.False(MongoIndexer.IsDocumentTypesWithExplicitMongoCollectionAttribute(typeof(ChildWithoutIndexerAssemblyScanningTestDocument)));
        Assert.True(MongoIndexer.IsDocumentTypesWithExplicitMongoCollectionAttribute(typeof(ChildWithOwnIndexerAssemblyScanningTestDocument)));
    }

    private sealed class RandomObject
    {
        public Guid WastedGuid { get; set; }
    }

    [BsonKnownTypes(typeof(ChildWithoutIndexerAssemblyScanningTestDocument))]
    [MongoCollection("assemblyScanner", IndexProviderType = typeof(BaseAssemblyScanningTestProvider))]
    private class BaseAssemblyScanningTestDocument : MongoDocument
    {
        [BsonElement("name")]
        public string Name { get; set; } = string.Empty;
    }

    [BsonDiscriminator("Children")]
    private sealed class ChildWithoutIndexerAssemblyScanningTestDocument : BaseAssemblyScanningTestDocument
    {
        [BsonElement("email")]
        public string Email { get; set; } = string.Empty;
    }

    [BsonDiscriminator("Children")]
    [MongoCollection("assemblyScanner", IndexProviderType = typeof(ChildWithOwnIndexerAssemblyScanningTestProvider))]
    private sealed class ChildWithOwnIndexerAssemblyScanningTestDocument : BaseAssemblyScanningTestDocument
    {
        [BsonElement("phone")]
        public string PhoneNumber { get; set; } = string.Empty;
    }

    private sealed class BaseAssemblyScanningTestProvider : MongoIndexProvider<BaseAssemblyScanningTestDocument>
    {
        public override IEnumerable<CreateIndexModel<BaseAssemblyScanningTestDocument>> CreateIndexModels()
        {
            yield return new CreateIndexModel<BaseAssemblyScanningTestDocument>(
                Builders<BaseAssemblyScanningTestDocument>.IndexKeys.Ascending(x => x.Name),
                new CreateIndexOptions { Name = "name" });
        }
    }

    private sealed class ChildWithOwnIndexerAssemblyScanningTestProvider : MongoIndexProvider<ChildWithOwnIndexerAssemblyScanningTestDocument>
    {
        public override IEnumerable<CreateIndexModel<ChildWithOwnIndexerAssemblyScanningTestDocument>> CreateIndexModels()
        {
            yield return new CreateIndexModel<ChildWithOwnIndexerAssemblyScanningTestDocument>(
                Builders<ChildWithOwnIndexerAssemblyScanningTestDocument>
                    .IndexKeys.Ascending(x => x.PhoneNumber),
                new CreateIndexOptions { Name = "contact" });
        }
    }
}
