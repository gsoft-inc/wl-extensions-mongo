using Workleap.Extensions.Mongo;

// ReSharper disable once CheckNamespace
namespace MongoDB.Driver;

public static class IMongoDatabaseExtensions
{
    public static IMongoCollection<TDocument> GetCollection<TDocument>(this IMongoDatabase database, MongoCollectionSettings? settings = null)
        where TDocument : class
    {
        return database.GetCollection<TDocument>(MongoCollectionInformationCache.GetCollectionName<TDocument>(), settings);
    }

    public static string GetCollectionName<TDocument>(this IMongoDatabase database)
        where TDocument : class
    {
        return MongoCollectionInformationCache.GetCollectionName<TDocument>();
    }
}