using System.Diagnostics.CodeAnalysis;
using MongoDB.Bson;
using MongoDB.Driver;

namespace GSoft.Infra.Mongo.Indexing;

internal interface IUniqueIndexNameFactory
{
    bool TryCreate<TDocument>(CreateIndexModel<TDocument> indexModel, [MaybeNullWhen(false)] out UniqueIndexName indexName);
    
    bool TryCreate(BsonValue indexDocument, [MaybeNullWhen(false)] out UniqueIndexName indexName);
}