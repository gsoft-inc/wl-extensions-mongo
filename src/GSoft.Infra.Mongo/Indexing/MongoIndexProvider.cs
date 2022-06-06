using MongoDB.Driver;

// ReSharper disable once CheckNamespace
namespace GSoft.Infra.Mongo;

/// <summary>
/// Inherit from this class to define indexes for a particular document type.
/// </summary>
public abstract class MongoIndexProvider<TDocument>
    where TDocument : IMongoDocument
{
    public abstract IEnumerable<CreateIndexModel<TDocument>> CreateIndexModels();
}