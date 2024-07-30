using Workleap.Extensions.Mongo;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace MongoDB.Driver;
#pragma warning restore IDE0130

public static class IMongoCollectionExtensions
{
    public static string GetName<TDocument>(this IMongoCollection<TDocument> collection)
        where TDocument : class
    {
        return MongoCollectionInformationCache.GetCollectionName<TDocument>();
    }
}