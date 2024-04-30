using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;

namespace Workleap.Extensions.Mongo.Tests;

public sealed class MongoCollectionBuilderTests
{
    private readonly MongoCollectionBuilder<TestDocument> _builder = new();

    [Fact]
    public async Task Builder_Builds_Correctly()
    {
        var collectionName = "collection1";

        Action<BsonClassMap<TestDocument>> classMapInitializer = map =>
        {
            map.MapIdProperty(x => x.Id);
        };
        
        this._builder.CollectionName(collectionName)
            .IndexProvider<TestDocumentIndexProvider>()
            .BsonClassMap(classMapInitializer);
            
        var metadata = this._builder.Build() as MongoCollectionMetadata<TestDocument>;
        Assert.NotNull(metadata);
        Assert.Equal(collectionName, metadata.CollectionName);
        Assert.Equal(typeof(TestDocumentIndexProvider), metadata.IndexProviderType);
        Assert.Equal(classMapInitializer, metadata.ClassMapInitializer);
    }

    public sealed class TestDocument
    {
        public string Id { get; set; } = ObjectId.GenerateNewId().ToString();
        
        public string Name { get; set; } = string.Empty;
    }
    
    public sealed class TestDocumentIndexProvider : MongoIndexProvider<TestDocument>
    {
        public override IEnumerable<CreateIndexModel<TestDocument>> CreateIndexModels()
        {
            yield return new CreateIndexModel<TestDocument>(this.IndexKeys.Ascending(x => x.Id));
        }
    }
}