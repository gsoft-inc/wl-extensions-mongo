using Workleap.Extensions.Mongo;

// ReSharper disable once CheckNamespace
namespace MongoDB.Driver;

public static class IMongoCollectionExtensions
{
    public static string GetName<TDocument>(this IMongoCollection<TDocument> collection)
        where TDocument : class
    {
        return MongoReflectionCache.GetCollectionName<TDocument>();
    }
}