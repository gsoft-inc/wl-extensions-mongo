using Workleap.Extensions.Xunit;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;
using Workleap.Extensions.Mongo.Indexing;

namespace Workleap.Extensions.Mongo.Tests;

public class MongoIndexerTests : BaseIntegrationTest<ConfigurationMongoFixture>
{
    private const string OtherDatabaseName = "other";

    public MongoIndexerTests(ConfigurationMongoFixture fixture, ITestOutputHelper testOutputHelper)
        : base(fixture, testOutputHelper)
    {
    }

    [Fact]
    public async Task Indexes_Are_Automatically_Created_When_Specifying_DocumentType()
    {
        await this.Services.GetRequiredService<IMongoIndexer>().UpdateIndexesAsync(new[] { typeof(PersonDocument), typeof(OtherDatabasePersonDocument) });
        await this.AssertPersonDocumentIndexes<PersonDocument>(this.Services.GetRequiredService<IMongoDatabase>().DatabaseNamespace.DatabaseName);
        await this.AssertPersonDocumentIndexes<OtherDatabasePersonDocument>(OtherDatabaseName);
    }

    [Fact]
    public async Task Indexes_Are_Automatically_Created_When_Specifying_Assembly()
    {
        await this.Services.GetRequiredService<IMongoIndexer>().UpdateIndexesAsync(typeof(PersonDocument).Assembly);
        await this.AssertPersonDocumentIndexes<PersonDocument>(this.Services.GetRequiredService<IMongoDatabase>().DatabaseNamespace.DatabaseName);
        await this.AssertPersonDocumentIndexes<OtherDatabasePersonDocument>(OtherDatabaseName);
    }

    private async Task AssertPersonDocumentIndexes<TDocument>(string expectedDatabaseName)
    {
        var collection = this.Services.GetRequiredService<IMongoCollection<TDocument>>();
        Assert.Equal(expectedDatabaseName, collection.Database.DatabaseNamespace.DatabaseName);

        using var personDocumentIndexCursor = await collection.Indexes.ListAsync();

        var personDocumentIndexNames = await personDocumentIndexCursor.ToAsyncEnumerable().Select(x => x["name"].AsString).ToListAsync();

        Assert.Equal(3, personDocumentIndexNames.Count);
        Assert.Contains("_id_", personDocumentIndexNames);
        Assert.Contains("fn_ln_15cfbc3bcdc8c4f800adf1709115006a", personDocumentIndexNames);
        Assert.Contains("age_7c4afaa70df651e198675bca5bcb6ad2", personDocumentIndexNames);

        using var personIndexCursor = await this.Services.GetRequiredService<IMongoCollection<ConfigurationMongoFixture.Person>>().Indexes.ListAsync();
        var personIndexNames = await personIndexCursor.ToAsyncEnumerable().Select(x => x["name"].AsString).ToListAsync();
        Assert.Equal(2, personIndexNames.Count);
        Assert.Contains("_id_", personIndexNames);
        Assert.Contains("IX_name_7bf595dc01e7866161710b1f448f5183", personIndexNames);
    }

    [MongoCollection("person", IndexProviderType = typeof(PersonDocumentIndexes))]
    private sealed class PersonDocument : MongoDocument
    {
        public string Firstname { get; set; } = string.Empty;

        public string Lastname { get; set; } = string.Empty;

        public int Age { get; set; }
    }

    [MongoCollection("person", DatabaseName = OtherDatabaseName, IndexProviderType = typeof(OtherDatabasePersonDocumentIndexes))]
    private sealed class OtherDatabasePersonDocument : MongoDocument
    {
        public string Firstname { get; set; } = string.Empty;

        public string Lastname { get; set; } = string.Empty;

        public int Age { get; set; }
    }

    private sealed class OtherDatabasePersonDocumentIndexes : MongoIndexProvider<OtherDatabasePersonDocument>
    {
        public override IEnumerable<CreateIndexModel<OtherDatabasePersonDocument>> CreateIndexModels()
        {
            yield return new CreateIndexModel<OtherDatabasePersonDocument>(
                Builders<OtherDatabasePersonDocument>.IndexKeys.Combine(
                    Builders<OtherDatabasePersonDocument>.IndexKeys.Ascending(x => x.Firstname),
                    Builders<OtherDatabasePersonDocument>.IndexKeys.Ascending(x => x.Lastname)),
                new CreateIndexOptions { Name = "fn_ln" });

            yield return new CreateIndexModel<OtherDatabasePersonDocument>(
                Builders<OtherDatabasePersonDocument>.IndexKeys.Ascending(x => x.Age),
                new CreateIndexOptions { Name = "age" });
        }
    }

    private sealed class PersonDocumentIndexes : MongoIndexProvider<PersonDocument>
    {
        public override IEnumerable<CreateIndexModel<PersonDocument>> CreateIndexModels()
        {
            yield return new CreateIndexModel<PersonDocument>(
                Builders<PersonDocument>.IndexKeys.Combine(
                    Builders<PersonDocument>.IndexKeys.Ascending(x => x.Firstname),
                    Builders<PersonDocument>.IndexKeys.Ascending(x => x.Lastname)),
                new CreateIndexOptions { Name = "fn_ln" });

            yield return new CreateIndexModel<PersonDocument>(
                Builders<PersonDocument>.IndexKeys.Ascending(x => x.Age),
                new CreateIndexOptions { Name = "age" });
        }
    }
}