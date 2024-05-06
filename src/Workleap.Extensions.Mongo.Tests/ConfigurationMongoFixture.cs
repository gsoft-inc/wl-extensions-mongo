using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;

namespace Workleap.Extensions.Mongo.Tests;

public sealed class ConfigurationMongoFixture : MongoFixture
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