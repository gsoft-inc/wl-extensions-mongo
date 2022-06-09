using ShareGate.Infra.Mongo.Indexing;
using ShareGate.Infra.Mongo.Logging;
using ShareGate.Infra.Mongo.Security;
using ShareGate.Infra.Mongo.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;
using MongoDB.Driver.Core.Events;

namespace ShareGate.Infra.Mongo;

public static class MongoServiceCollectionExtensions
{
    public static MongoBuilder AddMongo(this IServiceCollection services, Action<MongoOptions>? configure = null)
    {
        services.ConfigureOptions<InitialMongoSetup>();

        if (configure != null)
        {
            services.Configure(configure);
        }

        services.TryAddSingleton(CreateMongoClient);
        services.TryAddSingleton(CreateMongoDatabase);
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IEventSubscriber, MongoLoggingEventSubscriber>());
        services.TryAddSingleton<MongoInitializer>();
        services.TryAddSingleton<MongoDistributedLockFactory>();
        services.TryAddSingleton<IMongoIndexer, MongoIndexer>();
        services.TryAddSingleton<IMongoValueEncryptor, NoopMongoValueEncryptor>();
        services.TryAddSingleton<IUniqueIndexNameFactory, UniqueIndexNameFactory>();

        return new MongoBuilder(services);
    }

    private static IMongoClient CreateMongoClient(IServiceProvider serviceProvider)
    {
        serviceProvider.GetRequiredService<MongoInitializer>().Initialize();

        var options = serviceProvider.GetRequiredService<IOptions<MongoOptions>>();
        var settings = MongoClientSettings.FromConnectionString(options.Value.ConnectionString);

        if (options.Value.MinConnectionPoolSize is { } minConnectionPoolSize)
        {
            settings.MinConnectionPoolSize = minConnectionPoolSize;
        }

        if (options.Value.MaxConnectionPoolSize is { } maxConnectionPoolSize)
        {
            settings.MaxConnectionPoolSize = maxConnectionPoolSize;
        }

        var eventSubscribers = serviceProvider.GetServices<IEventSubscriber>();

        settings.ClusterConfigurator = builder =>
        {
            foreach (var eventSubscriber in eventSubscribers)
            {
                builder.Subscribe(eventSubscriber);
            }
        };

        return new MongoClient(settings);
    }

    private static IMongoDatabase CreateMongoDatabase(IServiceProvider serviceProvider)
    {
        var client = serviceProvider.GetRequiredService<IMongoClient>();
        var options = serviceProvider.GetRequiredService<IOptions<MongoOptions>>();
        return client.GetDatabase(options.Value.DefaultDatabaseName);
    }

    private sealed class InitialMongoSetup : IConfigureOptions<MongoOptions>
    {
        private readonly IMongoValueEncryptor _mongoValueEncryptor;

        public InitialMongoSetup(IMongoValueEncryptor mongoValueEncryptor)
        {
            this._mongoValueEncryptor = mongoValueEncryptor;
        }

        public void Configure(MongoOptions options)
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