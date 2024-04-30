using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;
using Workleap.Extensions.Mongo.Indexing;
using Workleap.Extensions.Xunit;

namespace Workleap.Extensions.Mongo.Tests;

public sealed class AddCollectionConfigurationTests : BaseIntegrationTest<AddCollectionConfigurationTests.AddCollectionConfigurationTestsFixture> 
{
    public AddCollectionConfigurationTests(AddCollectionConfigurationTestsFixture fixture, ITestOutputHelper testOutputHelper) : base(fixture, testOutputHelper)
    {
    }

    [Fact]
    public async Task AddCollectionConfigurations_Registers_CollectionNames_IndexProviders_BsonClassMaps_Correctly()
    {
        var indexer = this.Services.GetRequiredService<IMongoIndexer>();
        
        await indexer.UpdateIndexesAsync();
        
        var collection = this.Services.GetRequiredService<IMongoCollection<AddCollectionConfigurationTestsFixture.Person>>();

        Assert.Equal("People", collection.CollectionNamespace.CollectionName);

        var indexes = (await collection.Indexes.ListAsync()).ToList();
        Assert.Equal(2, indexes.Count);
        Assert.NotNull(indexes.SingleOrDefault(i => i.GetElement("name").Value.AsString.StartsWith("IX_name")));

        var personMap = BsonClassMap.GetRegisteredClassMaps().SingleOrDefault(map => map.ClassType == typeof(AddCollectionConfigurationTestsFixture.Person));
        
        Assert.NotNull(personMap);
        Assert.Equal(2, personMap.AllMemberMaps.Count);
        Assert.NotNull(personMap.AllMemberMaps.SingleOrDefault(m => m.ElementName == "_id"));
        Assert.NotNull(personMap.AllMemberMaps.SingleOrDefault(m => m.ElementName == "n"));
    }
    
    public sealed class AddCollectionConfigurationTestsFixture : MongoFixture
    {
        public override IServiceCollection ConfigureServices(IServiceCollection services)
        {
            base.ConfigureServices(services);

            services.AddMongo().AddCollectionConfigurations(typeof(AddCollectionConfigurationTests).Assembly);

            return services;
        }

        public sealed class Person
        {
            public string Id { get; set; } = ObjectId.GenerateNewId().ToString();

            public string Name { get; set; } = string.Empty;
        }

        public sealed class PersonConfiguration : IMongoCollectionConfiguration<Person>
        {
            public void Configure(IMongoCollectionBuilder<Person> builder)
            {
                builder.CollectionName("People")
                    .IndexProvider<PersonIndexProvider>()
                    .BsonClassMap(map =>
                    {
                        map.MapIdProperty(x => x.Id).SetSerializer(new StringSerializer(BsonType.ObjectId));
                        map.MapProperty(x => x.Name).SetElementName("n");
                    });
            }
        }

        public sealed class PersonIndexProvider : MongoIndexProvider<Person>
        {
            public override IEnumerable<CreateIndexModel<Person>> CreateIndexModels()
            {
                yield return new CreateIndexModel<Person>(this.IndexKeys.Ascending(x => x.Name), new CreateIndexOptions
                {
                    Name = "IX_name",
                });
            }
        }
    }
}