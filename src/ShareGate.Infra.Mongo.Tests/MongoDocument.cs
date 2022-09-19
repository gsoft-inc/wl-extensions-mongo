using System.Diagnostics.CodeAnalysis;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace ShareGate.Infra.Mongo.Tests;

public abstract class MongoDocument : IMongoDocument
{
    [BsonRepresentation(BsonType.String)]
    [BsonId(IdGenerator = typeof(StringGuidIdGenerator))]
    [SuppressMessage("ReSharper", "NullableWarningSuppressionIsUsed", Justification = "The ID will be generated on insertion and should not be used before.")]
    public string Id { get; set; } = null!;
}