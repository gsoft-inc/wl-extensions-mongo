using GSoft.Extensions.Mongo.Indexing;
using ShareGate.Extensions.Xunit;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;

namespace GSoft.Extensions.Mongo.Tests;

public sealed class MongoIndexerTests : BaseIntegrationTest<MongoFixture>
{
    public MongoIndexerTests(MongoFixture fixture, ITestOutputHelper testOutputHelper)
        : base(fixture, testOutputHelper)
    {
    }

    [Fact]
    public async Task Indexes_Are_Automatically_Created_When_Specifying_DocumentType()
    {
        await this.Services.GetRequiredService<IMongoIndexer>().UpdateIndexesAsync(new[] { typeof(PersonDocument) });
        await this.AssertPersonDocumentIndexes();
    }

    [Fact]
    public async Task Indexes_Are_Automatically_Created_When_Specifying_Assembly()
    {
        await this.Services.GetRequiredService<IMongoIndexer>().UpdateIndexesAsync(typeof(PersonDocument).Assembly);
        await this.AssertPersonDocumentIndexes();
    }

    private async Task AssertPersonDocumentIndexes()
    {
        using var indexCursor = await this.Services.GetRequiredService<IMongoCollection<PersonDocument>>().Indexes.ListAsync();
        var indexNames = await indexCursor.ToAsyncEnumerable().Select(x => x["name"].AsString).ToListAsync();

        Assert.Equal(3, indexNames.Count);
        Assert.Contains("_id_", indexNames);
        Assert.Contains("fn_ln_15cfbc3bcdc8c4f800adf1709115006a", indexNames);
        Assert.Contains("age_7c4afaa70df651e198675bca5bcb6ad2", indexNames);
    }

    [MongoCollection("person", IndexProviderType = typeof(PersonDocumentIndexes))]
    private sealed class PersonDocument : MongoDocument
    {
        public string Firstname { get; set; } = string.Empty;

        public string Lastname { get; set; } = string.Empty;

        public int Age { get; set; }
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