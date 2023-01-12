using MongoDB.Driver;

namespace GSoft.Extensions.Mongo.Indexing;

internal sealed class EmptyMongoIndexProvider<TDocument> : MongoIndexProvider<TDocument>
    where TDocument : IMongoDocument
{
    public override IEnumerable<CreateIndexModel<TDocument>> CreateIndexModels()
    {
        return Enumerable.Empty<CreateIndexModel<TDocument>>();
    }
}