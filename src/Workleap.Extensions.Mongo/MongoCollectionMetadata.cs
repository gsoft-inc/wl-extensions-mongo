using MongoDB.Bson.Serialization;

namespace Workleap.Extensions.Mongo;

internal abstract class MongoCollectionMetadata
{
    protected MongoCollectionMetadata(string collectionName)
    {
        this.CollectionName = collectionName;
    }

    public string CollectionName { get; internal set; }

    public Type? IndexProviderType { get; internal set; }
}

internal sealed class MongoCollectionMetadata<T> : MongoCollectionMetadata
    where T : class
{
    internal MongoCollectionMetadata() : base(typeof(T).Name)
    {
    }
    
    public Action<BsonClassMap<T>>? ClassMapInitializer { get; internal set; }
}