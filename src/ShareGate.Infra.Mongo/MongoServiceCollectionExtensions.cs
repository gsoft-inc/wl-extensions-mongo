using ShareGate.Infra.Mongo.Indexing;
using ShareGate.Infra.Mongo.Security;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;

namespace ShareGate.Infra.Mongo;

public static class MongoServiceCollectionExtensions
{
    public static MongoBuilder AddMongo(this IServiceCollection services, Action<MongoClientOptions>? configure = null)
    {
        services.ConfigureOptions<ConfigureMongoStaticOptions>();

        if (configure != null)
        {
            services.Configure(MongoDefaults.ClientName, configure);
        }

        services.TryAddSingleton<IMongoClientProvider, MongoClientProvider>();
        services.TryAddSingleton(CreateDefaultMongoClient);
        services.TryAddSingleton(CreateDefaultMongoDatabase);

        // Inspired from Microsoft's ILogger<> singleton service descriptor:
        // https://github.com/dotnet/runtime/blob/v6.0.0/src/libraries/Microsoft.Extensions.Logging/src/LoggingServiceCollectionExtensions.cs#L42
        services.TryAdd(ServiceDescriptor.Singleton(typeof(IMongoCollection<>), typeof(MongoCollectionProxy<>)));

        services.TryAddSingleton<MongoInitializer>();
        services.TryAddSingleton<IMongoIndexer, MongoIndexer>();
        services.TryAddSingleton<IMongoValueEncryptor, NoopMongoValueEncryptor>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IMongoEventSubscriberFactory, MongoEventSubscriberFactory>());

        return new MongoBuilder(services);
    }

    private static IMongoClient CreateDefaultMongoClient(IServiceProvider serviceProvider)
    {
        return serviceProvider.GetRequiredService<IMongoClientProvider>().GetClient(MongoDefaults.ClientName);
    }

    private static IMongoDatabase CreateDefaultMongoDatabase(IServiceProvider serviceProvider)
    {
        var client = serviceProvider.GetRequiredService<IMongoClient>();
        var options = serviceProvider.GetRequiredService<IOptionsMonitor<MongoClientOptions>>().Get(MongoDefaults.ClientName);
        return client.GetDatabase(options.DefaultDatabaseName);
    }

    private sealed class ConfigureMongoStaticOptions : IConfigureOptions<MongoStaticOptions>
    {
        private readonly IMongoValueEncryptor _mongoValueEncryptor;

        public ConfigureMongoStaticOptions(IMongoValueEncryptor mongoValueEncryptor)
        {
            this._mongoValueEncryptor = mongoValueEncryptor;
        }

        public void Configure(MongoStaticOptions options)
        {
            options.BsonSerializers[typeof(Guid)] = new GuidSerializer(GuidRepresentation.Standard);

            // By default, serialize .NET datetimes as a MongoDB datetimes instead of the default array format [ticks, offset]
            // Pros: very lightweight, easy to index, cpu and storage efficient, it's human-readable
            // Cons: MongoDB datetimes are precise to the millisecond, so we lose a bit of precision compared to storing ticks
            // Any document that require sub-millisecond precision can override the serializer at the property level.
            options.BsonSerializers[typeof(DateTime)] = new DateTimeSerializer(BsonType.DateTime);
            options.BsonSerializers[typeof(DateTimeOffset)] = new DateTimeOffsetSerializer(BsonType.DateTime);

            options.ConventionPacks.Add(new CommonConventionPack());
            options.ConventionPacks.Add(new SensitiveInformationConventionPack(this._mongoValueEncryptor));
        }
    }
}