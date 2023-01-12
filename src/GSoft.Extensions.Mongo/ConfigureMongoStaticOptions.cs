using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Serializers;
using GSoft.Extensions.Mongo.Security;

namespace GSoft.Extensions.Mongo;

internal sealed class ConfigureMongoStaticOptions : IConfigureOptions<MongoStaticOptions>
{
    private readonly IMongoValueEncryptor _mongoValueEncryptor;

    public ConfigureMongoStaticOptions(IMongoValueEncryptor mongoValueEncryptor)
    {
        this._mongoValueEncryptor = mongoValueEncryptor;
    }

    public void Configure(MongoStaticOptions options)
    {
        options.BsonSerializers[typeof(Guid)] = new GuidSerializer(GuidRepresentation.Standard);

        // By default, serialize .NET datetimes as a MongoDB datetimes instead of the default array format [ticks, offset]
        // Pros: very lightweight, easy to index, cpu and storage efficient, it's human-readable
        // Cons: MongoDB datetimes are precise to the millisecond, so we lose a bit of precision compared to storing ticks
        // Any document that require sub-millisecond precision can override the serializer at the property level.
        options.BsonSerializers[typeof(DateTime)] = new DateTimeSerializer(BsonType.DateTime);
        options.BsonSerializers[typeof(DateTimeOffset)] = new DateTimeOffsetSerializer(BsonType.DateTime);

        options.ConventionPacks.Add(new CommonConventionPack());
        options.ConventionPacks.Add(new SensitiveInformationConventionPack(this._mongoValueEncryptor));
    }
}