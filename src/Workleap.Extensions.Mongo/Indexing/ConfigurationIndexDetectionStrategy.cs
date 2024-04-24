namespace Workleap.Extensions.Mongo.Indexing;

internal sealed class ConfigurationIndexDetectionStrategy : IIndexDetectionStrategy
{
    private readonly MongoReflectionCacheConfigurationStrategy _cache;

    public ConfigurationIndexDetectionStrategy(MongoReflectionCacheConfigurationStrategy cache)
    {
        this._cache = cache;
    }
    
    public IReadOnlyList<Type> GetDocumentTypes(IEnumerable<Type> allTypes)
    {
        return allTypes.Where(t => t.GetInterfaces().Any(i => i.IsMongoCollectionConfigurationInterface()))
            .ToArray();
    }

    public void ValidateType(Type type)
    {
    }

    public IndexRegistry CreateRegistry(IEnumerable<Type> documentTypes)
    {
        return new ConfigurationIndexRegistry(documentTypes, this._cache.GetIndexProviderTypes());
    }
}