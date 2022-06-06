using System.Collections.Concurrent;
using System.Reflection;

namespace GSoft.Infra.Mongo;

internal static class MongoReflectionCache
{
    private static readonly ConcurrentDictionary<Type, string> CollectionNames = new ConcurrentDictionary<Type, string>();

    public static string GetCollectionName(Type documentType)
    {
        if (!IsConcreteMongoDocumentType(documentType))
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
        where TDocument : IMongoDocument
    {
        return GetCollectionName(typeof(TDocument));
    }

    public static bool IsConcreteMongoDocumentType(Type type) => !type.IsAbstract && typeof(IMongoDocument).IsAssignableFrom(type);
}