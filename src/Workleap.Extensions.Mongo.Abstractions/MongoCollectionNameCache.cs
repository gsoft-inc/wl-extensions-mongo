using System.Collections.Concurrent;
using System.Reflection;

namespace Workleap.Extensions.Mongo;

internal static class MongoCollectionNameCache
{
    private static readonly ConcurrentDictionary<Type, string> CollectionNames = new();

    public static string GetCollectionName(Type documentType)
    {
        if (CollectionNames.TryGetValue(documentType, out var collectionName))
        {
            return collectionName;
        }
        
        // Configuration based CollectionNames are set manually by calling SetCollectionName.
        // When we reach here, we can validate the Attribute flow because it was not a document from the Configuration flow.
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

            throw new ArgumentException(documentType + " must be decorated with " + nameof(MongoCollectionAttribute) + " or be registered by a " + typeof(IMongoCollectionConfiguration<>).MakeGenericType(documentType).Name);
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
}