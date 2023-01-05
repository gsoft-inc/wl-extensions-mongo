using MongoDB.Bson.Serialization;

namespace GSoft.Extensions.Mongo;

public sealed class MongoStaticOptions
{
    public MongoStaticOptions()
    {
        this.BsonSerializers = new Dictionary<Type, IBsonSerializer>();
        this.ConventionPacks = new List<NamedConventionPack>();
    }

    public IDictionary<Type, IBsonSerializer> BsonSerializers { get; }

    public IList<NamedConventionPack> ConventionPacks { get; }
}