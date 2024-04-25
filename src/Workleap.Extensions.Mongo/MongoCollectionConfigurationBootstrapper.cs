using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace Workleap.Extensions.Mongo;

public sealed class MongoCollectionConfigurationBootstrapper : IMongoCollectionConfigurationBootstrapper
{
    private static readonly MethodInfo ConfigureMethod = typeof(MongoCollectionConfigurationBootstrapper).GetMethod(nameof(Configure), BindingFlags.NonPublic | BindingFlags.Static)
                                                            ?? throw new InvalidOperationException($"Could not find public instance method {nameof(MongoCollectionConfigurationBootstrapper)}.{nameof(Configure)}");
    
    private readonly IServiceProvider _serviceProvider;

    public MongoCollectionConfigurationBootstrapper(IServiceProvider serviceProvider)
    {
        this._serviceProvider = serviceProvider;
    }
    
    public void ApplyConfigurations(params Assembly[] assemblies)
    {
        var configurationCache = this._serviceProvider.GetRequiredService<MongoReflectionCacheConfigurationStrategy>();

        var configurationInterfaces = assemblies.SelectMany(assembly => assembly.GetTypes()
            .Where(t => !t.IsAbstract)
            .Select(t => t.GetInterfaces().FirstOrDefault(i => i.IsMongoCollectionConfigurationInterface()))
            .Where(t => t != null));

        foreach (var configurationInterface in configurationInterfaces)
        {
            var documentType = configurationInterface!.GetGenericArguments().Single();
            var builderType = typeof(MongoCollectionBuilder<>).MakeGenericType(documentType);

            var configuration = this._serviceProvider.GetRequiredService(configurationInterface);
            var builder = Activator.CreateInstance(builderType) as MongoCollectionBuilder;
            
            if (configuration == null || builder == null)
            {
                throw new InvalidOperationException($"Could not find configuration or builder for type {documentType}");
            }

            var configureMethod = ConfigureMethod.MakeGenericMethod(documentType);
            configureMethod.Invoke(null, new[] { configuration, builder });

            var metadata = builder.Build(); // BsonClassMap registration happens here
            
            configurationCache.SetCollectionName(documentType, metadata.CollectionName);
            
            if (metadata.IndexProviderType != null)
            {
                configurationCache.AddIndexProviderType(documentType, metadata.IndexProviderType);
            }
        }
    }
    
    private static void Configure<TDocument>(IMongoCollectionConfiguration<TDocument> configuration, IMongoCollectionBuilder<TDocument> builder)
        where TDocument : class
    {
        configuration.Configure(builder);
    }
}