using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using Workleap.Extensions.Mongo.Indexing;
using Workleap.Extensions.Xunit;

namespace Workleap.Extensions.Mongo.Tests;

public sealed class AddCollectionConfigurationTests : BaseIntegrationTest<ConfigurationMongoFixture>
{
    public AddCollectionConfigurationTests(ConfigurationMongoFixture fixture, ITestOutputHelper testOutputHelper) : base(fixture, testOutputHelper)
    {
    }

    [Fact]
    public async Task AddCollectionConfigurations_Registers_CollectionNames_IndexProviders_BsonClassMaps_Correctly()
    {
        var indexer = this.Services.GetRequiredService<IMongoIndexer>();

        await indexer.UpdateIndexesAsync();

        var collection = this.Services.GetRequiredService<IMongoCollection<ConfigurationMongoFixture.Person>>();

        Assert.Equal("People", collection.CollectionNamespace.CollectionName);

        var indexes = (await collection.Indexes.ListAsync()).ToList();
        Assert.Equal(2, indexes.Count);
        Assert.NotNull(indexes.SingleOrDefault(i => i.GetElement("name").Value.AsString.StartsWith("IX_name")));

        var personMap = BsonClassMap.GetRegisteredClassMaps().SingleOrDefault(map => map.ClassType == typeof(ConfigurationMongoFixture.Person));

        Assert.NotNull(personMap);
        Assert.Equal(2, personMap.AllMemberMaps.Count);
        Assert.NotNull(personMap.AllMemberMaps.SingleOrDefault(m => m.ElementName == "_id"));
        Assert.NotNull(personMap.AllMemberMaps.SingleOrDefault(m => m.ElementName == "n"));
    }

    [Fact]
    public void MultipleAddCollectionConfigurations_DoesNotCrash()
    {
        var services = new ServiceCollection();
        services.AddMongo().AddCollectionConfigurations(typeof(AddCollectionConfigurationTests).Assembly);
        services.AddMongo().AddCollectionConfigurations(typeof(AddCollectionConfigurationTests).Assembly);
    }
}