using Workleap.Extensions.Mongo;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace MongoDB.Driver;
#pragma warning restore IDE0130 

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