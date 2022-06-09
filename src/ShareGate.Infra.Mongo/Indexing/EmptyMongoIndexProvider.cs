using MongoDB.Driver;

namespace ShareGate.Infra.Mongo.Indexing;

internal sealed class EmptyMongoIndexProvider<TDocument> : MongoIndexProvider<TDocument>
    where TDocument : IMongoDocument
{
    public override IEnumerable<CreateIndexModel<TDocument>> CreateIndexModels()
    {
        return Enumerable.Empty<CreateIndexModel<TDocument>>();
    }
}