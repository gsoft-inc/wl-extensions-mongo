using MongoDB.Driver;

namespace Workleap.Extensions.Mongo.Indexing;

internal sealed class EmptyMongoIndexProvider<TDocument> : MongoIndexProvider<TDocument>
    where TDocument : class
{
    public override IEnumerable<CreateIndexModel<TDocument>> CreateIndexModels()
    {
        return Enumerable.Empty<CreateIndexModel<TDocument>>();
    }
}