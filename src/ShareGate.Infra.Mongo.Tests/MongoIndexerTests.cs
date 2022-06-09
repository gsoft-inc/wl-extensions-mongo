using ShareGate.Infra.Mongo.Indexing;
using ShareGate.Extensions.Xunit;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;
using Xunit;
using Xunit.Abstractions;

namespace ShareGate.Infra.Mongo.Tests;

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
        await this.Services.GetRequiredService<IMongoIndexer>().UpdateIndexesAsync(new[] { typeof(PersonDocument).Assembly });
        await this.AssertPersonDocumentIndexes();
    }

    private async Task AssertPersonDocumentIndexes()
    {
        using var indexCursor = await this.Services.GetRequiredService<IMongoDatabase>().GetCollection<PersonDocument>().Indexes.ListAsync();
        var indexNames = await indexCursor.ToAsyncEnumerable().Select(x => x["name"].AsString).ToListAsync();

        Assert.Equal(3, indexNames.Count);
        Assert.Contains("_id_", indexNames);
        Assert.Contains("fn_ln_15cfbc3bcdc8c4f800adf1709115006aff3a9596e6a6280cf4ec7de1e0c4afe9_1.2.3.0", indexNames);
        Assert.Contains("age_7c4afaa70df651e198675bca5bcb6ad2c60d88b31d0f6d966bcf9590896434ea_1.2.3.0", indexNames);
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
                    Builders<PersonDocument>.IndexKeys.Combine().Ascending(x => x.Firstname),
                    Builders<PersonDocument>.IndexKeys.Combine().Ascending(x => x.Lastname)),
                new CreateIndexOptions { Name = "fn_ln" });

            yield return new CreateIndexModel<PersonDocument>(
                Builders<PersonDocument>.IndexKeys.Combine().Ascending(x => x.Age),
                new CreateIndexOptions { Name = "age" });
        }
    }
}