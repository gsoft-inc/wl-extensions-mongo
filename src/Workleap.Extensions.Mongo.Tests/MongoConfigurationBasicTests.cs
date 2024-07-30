using Workleap.Extensions.Xunit;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Workleap.Extensions.Mongo.Tests;

public sealed class MongoConfigurationBasicTests : BaseIntegrationTest<ConfigurationMongoFixture>
{
    public MongoConfigurationBasicTests(ConfigurationMongoFixture fixture, ITestOutputHelper testOutputHelper)
        : base(fixture, testOutputHelper)
    {
    }

    [Fact]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1861:Avoid constant arrays as arguments", Justification = "Performance is not important in tests")]
    public async Task Configuration_Works_In_Database()
    {
        var people = new List<ConfigurationMongoFixture.Person>
        {
            new() { Name = "Mathieu" },
            new() { Name = "Jules" },
        };

        var collection = this.Services.GetRequiredService<IMongoCollection<ConfigurationMongoFixture.Person>>();
        await collection.InsertManyAsync(people);

        var insertedPeople = await collection.Find(FilterDefinition<ConfigurationMongoFixture.Person>.Empty).ToListAsync();

        Assert.Equal(people.Count, insertedPeople.Count);
        Assert.Single(insertedPeople, x => x.Name == "Mathieu");
        Assert.Single(insertedPeople, x => x.Name == "Jules");

        var rawPeople = await collection.Database.GetCollection<BsonDocument>("People").Find(FilterDefinition<BsonDocument>.Empty).ToListAsync();
        Assert.All(rawPeople, document => Assert.Equivalent(document.Names, new[] { "_id", "n" }));
    }
}