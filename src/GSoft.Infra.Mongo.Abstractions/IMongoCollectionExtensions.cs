using GSoft.Infra.Mongo;

// ReSharper disable once CheckNamespace
namespace MongoDB.Driver;

public static class IMongoCollectionExtensions
{
    public static string GetName<TDocument>(this IMongoCollection<TDocument> collection)
        where TDocument : IMongoDocument
    {
        return MongoReflectionCache.GetCollectionName<TDocument>();
    }
}