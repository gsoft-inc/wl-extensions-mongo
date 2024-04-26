using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using Workleap.Extensions.Mongo.Indexing;
using Workleap.Extensions.Mongo.Security;

namespace Workleap.Extensions.Mongo;

public static class MongoServiceCollectionExtensions
{
    private static readonly MethodInfo ConfigureMethod = typeof(MongoCollectionConfigurationBootstrapper).GetMethod(nameof(Configure), BindingFlags.NonPublic | BindingFlags.Static)
                                                         ?? throw new InvalidOperationException($"Could not find public instance method {nameof(MongoCollectionConfigurationBootstrapper)}.{nameof(Configure)}");
    
    public static MongoBuilder AddMongo(this IServiceCollection services, Action<MongoClientOptions>? configure = null)
    {
        services.ConfigureOptions<ConfigureMongoStaticOptions>();

        if (configure != null) services.Configure(MongoDefaults.ClientName, configure);

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
        services.TryAddSingleton<IIndexDetectionStrategy, AttributeIndexDetectionStrategy>();
        services.TryAddSingleton<IMongoValueEncryptor, NoopMongoValueEncryptor>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IMongoEventSubscriberFactory, MongoEventSubscriberFactory>());

        return new MongoBuilder(services);
    }

    public static MongoBuilder AddCollectionConfigurations(this MongoBuilder builder, params Assembly[] assemblies)
    {
        var configurationCache = new MongoReflectionCacheConfigurationStrategy();

        MongoReflectionCache.SetStrategy(configurationCache);

        builder.Services.AddSingleton(configurationCache);
        builder.Services.AddSingleton<IIndexDetectionStrategy, ConfigurationIndexDetectionStrategy>();

        var configurationTypes = assemblies.SelectMany(assembly => assembly.GetTypes()
            .Where(t => !t.IsAbstract)
            .Select(t => (ConcreteType: t, Interface: t.GetInterfaces().FirstOrDefault(i => i.IsMongoCollectionConfigurationInterface())))
            .Where(t => t.Interface != null));

        foreach (var configurationType in configurationTypes)
        {
            var (concreteType, configurationInterface) = configurationType;

            concreteType.EnsureHasPublicParameterlessConstructor();
            
            var documentType = configurationInterface!.GetGenericArguments().Single();
            var builderType = typeof(MongoCollectionBuilder<>).MakeGenericType(documentType);
            
            var configuration = Activator.CreateInstance(concreteType);

            if (Activator.CreateInstance(builderType) is not MongoCollectionBuilder configurationBuilder)
            {
                throw new InvalidOperationException($"Cannot create MongoCollectionBuilder<{documentType}>");
            }
            
            var configureMethod = ConfigureMethod.MakeGenericMethod(documentType);
            configureMethod.Invoke(null, new[] { configuration, builder });

            var metadata = configurationBuilder.Build(); // BsonClassMap registration happens here

            configurationCache.SetCollectionName(documentType, metadata.CollectionName);

            if (metadata.IndexProviderType != null)
            {
                configurationCache.AddIndexProviderType(documentType, metadata.IndexProviderType);
            }
        }

        return builder;
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
    
    private static void Configure<TDocument>(IMongoCollectionConfiguration<TDocument> configuration, IMongoCollectionBuilder<TDocument> builder)
        where TDocument : class
    {
        configuration.Configure(builder);
    }
}