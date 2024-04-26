using System.Collections.Concurrent;
using System.Reflection;

namespace Workleap.Extensions.Mongo;

internal static class MongoReflectionCache
{
    private static readonly ConcurrentDictionary<Type, string> CollectionNames = new();
    private static readonly ConcurrentDictionary<Type, Type> IndexProviderTypes = new();

    public static string GetCollectionName(Type documentType)
    {
        if (CollectionNames.TryGetValue(documentType, out var collectionName))
        {
            return collectionName;
        }
        
        if (!documentType.IsConcreteMongoDocumentType())
        {
            throw new ArgumentException(documentType + " must be a concrete type that implements " + nameof(IMongoDocument));
        }

        return CollectionNames.GetOrAdd(documentType, static documentType =>
        {
            if (documentType.GetCustomAttribute<MongoCollectionAttribute>() is { } attribute)
            {
                return attribute.Name;
            }

            throw new ArgumentException(documentType + " must be decorated with " + nameof(MongoCollectionAttribute));
        });
    }

    public static string GetCollectionName<TDocument>()
        where TDocument : class
    {
        return GetCollectionName(typeof(TDocument));
    }
    
    internal static void SetCollectionName(Type documentType, string collectionName)
    {
        if (!CollectionNames.TryAdd(documentType, collectionName))
        {
            throw new ArgumentException($"Collection name for {documentType} already set.");            
        }
    }
    
    internal static void AddIndexProviderType(Type documentType, Type indexProviderType)
    {
        if (!IndexProviderTypes.TryAdd(documentType, indexProviderType))
        {
            throw new ArgumentException($"IndexProviderType for {documentType} already set.");
        }
    }

    internal static IReadOnlyDictionary<Type, Type> GetIndexProviderTypes() => IndexProviderTypes;
}