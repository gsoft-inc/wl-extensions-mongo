using GSoft.Extensions.Mongo.Indexing;
using GSoft.Extensions.Mongo.Security;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace GSoft.Extensions.Mongo;

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

        // MongoDB C# driver documentation says that IMongoClient, IMongoDatabase and IMongoCollection<> are thread-safe and can be stored globally (i.e. as singletons):
        // https://mongodb.github.io/mongo-csharp-driver/2.10/reference/driver/connecting/
        services.TryAddSingleton(CreateDefaultMongoClient);
        services.TryAddSingleton(CreateDefaultMongoDatabase);

        // Inspired from Microsoft's ILogger<> singleton service descriptor:
        // https://github.com/dotnet/runtime/blob/v6.0.0/src/libraries/Microsoft.Extensions.Logging/src/LoggingServiceCollectionExtensions.cs#L42
        services.TryAdd(ServiceDescriptor.Singleton(typeof(IMongoCollection<>), typeof(MongoCollectionProxy<>)));

        services.TryAddSingleton<MongoStaticInitializer>();
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
}