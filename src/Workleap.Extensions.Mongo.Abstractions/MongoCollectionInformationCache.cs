using System.Collections.Concurrent;
using System.Reflection;

namespace Workleap.Extensions.Mongo;

internal static class MongoCollectionInformationCache
{
    private static readonly ConcurrentDictionary<Type, MongoCollectionInformation> CollectionInfos = new();

    public static string GetCollectionName(Type documentType)
    {
        return GetCollectionInformation(documentType).CollectionName;
    }

    public static string GetCollectionName<TDocument>()
        where TDocument : class
    {
        return GetCollectionName(typeof(TDocument));
    }

    public static MongoCollectionInformation GetCollectionInformation(Type documentType)
    {
        if (CollectionInfos.TryGetValue(documentType, out var collectionInfo))
        {
            return collectionInfo;
        }

        // Configuration based CollectionNames are set manually by calling SetCollectionInformation.
        // When we reach here, we can validate the Attribute flow because it was not a document from the Configuration flow.
        if (!documentType.IsConcreteMongoDocumentType())
        {
            throw new ArgumentException(documentType + " must be a concrete type that implements " + nameof(IMongoDocument));
        }

        return CollectionInfos.GetOrAdd(documentType, static documentType =>
        {
            if (documentType.GetCustomAttribute<MongoCollectionAttribute>() is { } attribute)
            {
                return new MongoCollectionInformation(documentType, attribute.Name, attribute?.DatabaseName, false);
            }

            throw new ArgumentException(documentType + " must be decorated with " + nameof(MongoCollectionAttribute) + " or be registered by a " + typeof(IMongoCollectionConfiguration<>).MakeGenericType(documentType).Name);
        });
    }

    internal static void SetCollectionInformation(Type documentType, string collectionName, string? databaseName)
    {
        if (!CollectionInfos.TryAdd(documentType, new MongoCollectionInformation(documentType, collectionName, databaseName, true)))
        {
            throw new ArgumentException($"Collection name for {documentType} already set.");
        }
    }

    public sealed class MongoCollectionInformation
    {
        public MongoCollectionInformation(Type documentType, string collectionName, string? databaseName, bool isRegisteredByConfiguration)
        {
            this.CollectionName = collectionName;
            this.DatabaseName = databaseName;
            this.DocumentType = documentType;
            this.IsRegisteredByConfiguration = isRegisteredByConfiguration;

        }

        public string CollectionName { get; set; } = string.Empty;

        public string? DatabaseName { get; set; }

        public Type DocumentType { get; set; }

        public bool IsRegisteredByConfiguration { get; set; }
    }
}