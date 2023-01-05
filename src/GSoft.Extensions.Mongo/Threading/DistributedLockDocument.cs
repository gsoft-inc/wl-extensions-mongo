using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace GSoft.Extensions.Mongo.Threading;

[MongoCollection("distributedLocks")]
internal sealed class DistributedLockDocument : IMongoDocument
{
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    public string Id { get; set; } = string.Empty;

    [BsonElement("acq")]
    public bool IsAcquired { get; set; }

    [BsonElement("oid")]
    public string OwnerId { get; set; } = string.Empty;

    [BsonElement("exp")]
    public long ExpiresAt { get; set; }
}