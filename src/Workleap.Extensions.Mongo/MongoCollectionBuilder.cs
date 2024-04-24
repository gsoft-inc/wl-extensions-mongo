using MongoDB.Bson.Serialization;

namespace Workleap.Extensions.Mongo;

public abstract class MongoCollectionBuilder
{
    internal abstract MongoCollectionMetadata Build();
}

public sealed class MongoCollectionBuilder<TDocument> : MongoCollectionBuilder, IMongoCollectionBuilder<TDocument>
    where TDocument : class
{
    private readonly MongoCollectionMetadata<TDocument> _metadata = new();
    
    public IMongoCollectionBuilder<TDocument> CollectionName(string collectionName)
    {
        this._metadata.CollectionName = collectionName;
        return this;
    }
    
    public IMongoCollectionBuilder<TDocument> IndexProvider<TIndexProvider>()
        where TIndexProvider : MongoIndexProvider<TDocument>
    {
        this._metadata.IndexProviderType = typeof(TIndexProvider);
        return this;
    }

    public IMongoCollectionBuilder<TDocument> BsonClassMap(Action<BsonClassMap<TDocument>> classMapInitializer)
    {
        this._metadata.ClassMapInitializer = classMapInitializer;
        return this;
    }

    internal override MongoCollectionMetadata Build()
    {
        if (this._metadata.ClassMapInitializer != null)
        {
            // It is very important that the registration of class maps occur prior to them being needed.
            // The best place to register them is at app startup prior to initializing a connection with MongoDB.
            MongoDB.Bson.Serialization.BsonClassMap.RegisterClassMap(this._metadata.ClassMapInitializer);
        }

        return this._metadata;
    }
}