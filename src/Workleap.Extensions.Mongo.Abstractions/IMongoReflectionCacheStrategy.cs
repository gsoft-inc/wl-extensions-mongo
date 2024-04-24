namespace Workleap.Extensions.Mongo;

internal interface IMongoReflectionCacheStrategy
{
    string GetCollectionName(Type documentType);
}