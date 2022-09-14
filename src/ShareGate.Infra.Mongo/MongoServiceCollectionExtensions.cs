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
using ShareGate.Infra.Mongo.Performance;

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

        // MongoDB C# driver documentation says that IMongoClient, IMongoDatabase and IMongoCollection<> are thread-safe and can be stored globally (i.e. as singletons):
        // https://mongodb.github.io/mongo-csharp-driver/2.10/reference/driver/connecting/
        services.TryAddSingleton(CreateMongoClient);
        services.TryAddSingleton(CreateMongoDatabase);

        // Inspired from Microsoft's ILogger<> singleton service descriptor:
        // https://github.com/dotnet/runtime/blob/v6.0.0/src/libraries/Microsoft.Extensions.Logging/src/LoggingServiceCollectionExtensions.cs#L42
        services.TryAdd(ServiceDescriptor.Singleton(typeof(IMongoCollection<>), typeof(MongoCollectionProxy<>)));

        services.TryAddEnumerable(ServiceDescriptor.Singleton<IEventSubscriber, CommandLoggingEventSubscriber>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IEventSubscriber, CommandPerformanceEventSubscriber>());
        services.TryAddSingleton<CommandPerformanceAnalyzer>();

        services.TryAddSingleton<MongoInitializer>();
        services.TryAddSingleton<MongoDistributedLockFactory>();
        services.TryAddSingleton<IMongoIndexer, MongoIndexer>();
        services.TryAddSingleton<IMongoValueEncryptor, NoopMongoValueEncryptor>();

        return new MongoBuilder(services);
    }

    private static IMongoClient CreateMongoClient(IServiceProvider serviceProvider)
    {
        serviceProvider.GetRequiredService<MongoInitializer>().Initialize();

        var options = serviceProvider.GetRequiredService<IOptions<MongoOptions>>();
        var settings = MongoClientSettings.FromConnectionString(options.Value.ConnectionString);

        // Default socket timeout is infinite. 60 seconds is the timeout used by OV.
        // Keeping infinite timeout means we could wait up to 15 minutes or even more (as seen in SG Cloud Copy)
        if (settings.SocketTimeout == TimeSpan.Zero)
        {
            settings.SocketTimeout = TimeSpan.FromSeconds(60);
        }

        // Default connect timeout is 30 seconds. 10 seconds is the timeout used by OV
        if (settings.ConnectTimeout == TimeSpan.FromSeconds(30))
        {
            settings.ConnectTimeout = TimeSpan.FromSeconds(10);
        }

        var eventSubscribers = serviceProvider.GetServices<IEventSubscriber>();

        settings.ClusterConfigurator = builder =>
        {
            foreach (var eventSubscriber in eventSubscribers)
            {
                builder.Subscribe(eventSubscriber);
            }
        };

        // Allow devs from overriding these settings
        options.Value.MongoClientSettingsConfigurator?.Invoke(settings);

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