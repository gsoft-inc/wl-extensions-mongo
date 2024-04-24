using System.Collections.Concurrent;
using System.Reflection;
using Amazon.Runtime.Documents;

namespace Workleap.Extensions.Mongo;

internal static class MongoReflectionCache
{
    private static IMongoReflectionCacheStrategy _strategy = new MongoReflectionCacheAttributeStrategy();

    public static string GetCollectionName(Type documentType)
    {
        return _strategy.GetCollectionName(documentType);
    }

    public static string GetCollectionName<TDocument>()
        where TDocument : class
    {
        return GetCollectionName(typeof(TDocument));
    }
    
    internal static void SetStrategy(IMongoReflectionCacheStrategy strategy)
    {
        _strategy = strategy;
    }
    
    internal static bool IsConcreteMongoDocumentType(this Type type) => !type.IsAbstract && typeof(IMongoDocument).IsAssignableFrom(type);
    
    internal static bool IsMongoCollectionConfigurationInterface(this Type t) => t.IsGenericType && t.GetGenericTypeDefinition() == typeof(IMongoCollectionConfiguration<>);
}

internal sealed class MongoReflectionCacheConfigurationStrategy : IMongoReflectionCacheStrategy
{
    private readonly ConcurrentDictionary<Type, string> _collectionNames = new();
    private readonly ConcurrentDictionary<Type, Type> _indexProviderTypes = new();

    public string GetCollectionName(Type documentType)
    {
        return this._collectionNames.TryGetValue(documentType, out var collectionName) 
            ? collectionName 
            : throw new ArgumentException($"No {typeof(IMongoCollectionConfiguration<>)} registered for {documentType}.");
    }
    
    internal void SetCollectionName(Type documentType, string collectionName)
    {
        if (!this._collectionNames.TryAdd(documentType, collectionName))
        {
            throw new ArgumentException($"Collection name for {documentType} already set.");            
        }
    }

    internal void AddIndexProviderType(Type documentType, Type indexProviderType)
    {
        if (!this._indexProviderTypes.TryAdd(documentType, indexProviderType))
        {
            throw new ArgumentException($"IndexProviderType for {documentType} already set.");
        }
    }

    internal IReadOnlyDictionary<Type, Type> GetIndexProviderTypes() => this._indexProviderTypes;
}

internal sealed class MongoReflectionCacheAttributeStrategy : IMongoReflectionCacheStrategy
{
    private readonly ConcurrentDictionary<Type, string> _collectionNames = new();

    public string GetCollectionName(Type documentType)
    {
        if (!documentType.IsConcreteMongoDocumentType())
        {
            throw new ArgumentException(documentType + " must be a concrete type that implements " + nameof(IMongoDocument));
        }

        return this._collectionNames.GetOrAdd(documentType, static documentType =>
        {
            if (documentType.GetCustomAttribute<MongoCollectionAttribute>() is { } attribute)
            {
                return attribute.Name;
            }

            throw new ArgumentException(documentType + " must be decorated with " + nameof(MongoCollectionAttribute));
        });
    }
}