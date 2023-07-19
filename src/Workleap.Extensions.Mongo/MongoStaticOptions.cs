using MongoDB.Bson;
using MongoDB.Bson.Serialization;

namespace Workleap.Extensions.Mongo;

public sealed class MongoStaticOptions
{
    public MongoStaticOptions()
    {
        this.BsonSerializers = new Dictionary<Type, IBsonSerializer>();
        this.ConventionPacks = new List<NamedConventionPack>();

        // Guid representation V3 will be the default in Mongo C# driver 3.x so we use it already (V2 is deprecated)
        // https://mongodb.github.io/mongo-csharp-driver/2.18/reference/bson/guidserialization/guidrepresentationmode/guidrepresentationmode/
        this.GuidRepresentationMode = GuidRepresentationMode.V3;
    }

    public GuidRepresentationMode GuidRepresentationMode { get; set; }

    public IDictionary<Type, IBsonSerializer> BsonSerializers { get; }

    public IList<NamedConventionPack> ConventionPacks { get; }
}