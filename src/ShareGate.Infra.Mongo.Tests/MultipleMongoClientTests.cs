using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;
using ShareGate.Extensions.Xunit;
using ShareGate.Infra.Mongo.Indexing;
using Xunit;
using Xunit.Abstractions;

namespace ShareGate.Infra.Mongo.Tests;

[Collection("performance")]
public sealed class MultipleMongoClientTests : BaseIntegrationTest<MultipleMongoClientTests.MultipleMongoClientFixture>
{
    private const string FooClient = "foo";
    private const string BarClient = "bar";

    public MultipleMongoClientTests(MultipleMongoClientFixture fixture, ITestOutputHelper testOutputHelper)
        : base(fixture, testOutputHelper)
    {
    }

    [Fact]
    public async Task AddMultipleClients_Works()
    {
        // Verify that we can create three distinct MongoClients that target different MongoDB instances
        var defaultClient = this.Services.GetRequiredService<IMongoClient>();
        var fooClient = this.Services.GetRequiredService<IMongoClientProvider>().GetClient(FooClient);
        var barClient = this.Services.GetRequiredService<IMongoClientProvider>().GetClient(BarClient);

        Assert.Equal("app1", defaultClient.Settings.ApplicationName);
        Assert.Equal("app2", fooClient.Settings.ApplicationName);
        Assert.Equal("app3", barClient.Settings.ApplicationName);

        // Verify that we have three databases and three collections with the same name, but they're on different MongoDB instances
        const string sameDatabaseName = "somedb";
        var defaultCatCollection = defaultClient.GetDatabase(sameDatabaseName).GetCollection<CatDocument>();
        var fooCatCollection = fooClient.GetDatabase(sameDatabaseName).GetCollection<CatDocument>();
        var barCatCollection = barClient.GetDatabase(sameDatabaseName).GetCollection<CatDocument>();

        // Voluntarily omit to update indexes on bar's database to prove that we use multiple MongoClients
        var indexer = this.Services.GetRequiredService<IMongoIndexer>();
        await indexer.UpdateIndexesAsync(new[] { typeof(CatDocument) }, databaseName: sameDatabaseName);
        await indexer.UpdateIndexesAsync(new[] { typeof(CatDocument) }, clientName: FooClient, databaseName: sameDatabaseName);

        // Can't duplicate the cat on default and foo collection because they have an unique index on the name
        const string sameCatName = "somename";
        await defaultCatCollection.InsertOneAsync(new CatDocument { Name = sameCatName });
        var ex1 = await Assert.ThrowsAsync<MongoWriteException>(() => defaultCatCollection.InsertOneAsync(new CatDocument { Name = sameCatName }));
        Assert.Equal(ServerErrorCategory.DuplicateKey, ex1.WriteError.Category);

        await fooCatCollection.InsertOneAsync(new CatDocument { Name = sameCatName });
        var ex2 = await Assert.ThrowsAsync<MongoWriteException>(() => fooCatCollection.InsertOneAsync(new CatDocument { Name = sameCatName }));
        Assert.Equal(ServerErrorCategory.DuplicateKey, ex2.WriteError.Category);

        await barCatCollection.InsertOneAsync(new CatDocument { Name = sameCatName });
        await barCatCollection.InsertOneAsync(new CatDocument { Name = sameCatName });
    }

    public sealed class MultipleMongoClientFixture : MongoFixture
    {
        public override IServiceCollection ConfigureServices(IServiceCollection services)
        {
            base.ConfigureServices(services);

            services
                .AddMongo(options => options.MongoClientSettingsConfigurator = x => x.ApplicationName = "app1")
                .AddNamedClient(FooClient, options => options.MongoClientSettingsConfigurator = x => x.ApplicationName = "app2")
                .AddNamedClient(BarClient, options => options.MongoClientSettingsConfigurator = x => x.ApplicationName = "app3");

            return services;
        }
    }

    [MongoCollection("cats", IndexProviderType = typeof(CatDocumentIndexes))]
    private sealed class CatDocument : MongoDocument
    {
        public string Name { get; set; } = string.Empty;
    }

    private sealed class CatDocumentIndexes : MongoIndexProvider<CatDocument>
    {
        public override IEnumerable<CreateIndexModel<CatDocument>> CreateIndexModels()
        {
            yield return new CreateIndexModel<CatDocument>(
                Builders<CatDocument>.IndexKeys.Ascending(x => x.Name),
                new CreateIndexOptions { Name = "name", Unique = true });
        }
    }
}