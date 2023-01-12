using GSoft.Extensions.Mongo;

// ReSharper disable once CheckNamespace
namespace MongoDB.Driver;

public static class IMongoDatabaseExtensions
{
    public static IMongoCollection<TDocument> GetCollection<TDocument>(this IMongoDatabase database, MongoCollectionSettings? settings = null)
        where TDocument : IMongoDocument
    {
        return database.GetCollection<TDocument>(MongoReflectionCache.GetCollectionName<TDocument>(), settings);
    }

    public static string GetCollectionName<TDocument>(this IMongoDatabase database)
        where TDocument : IMongoDocument
    {
        return MongoReflectionCache.GetCollectionName<TDocument>();
    }
}