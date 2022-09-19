using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Conventions;

namespace ShareGate.Infra.Mongo;

internal sealed class MongoStaticInitializer
{
    private static readonly object _lockObject = new object();
    private static bool _initialized;

    private readonly IOptions<MongoStaticOptions> _options;

    public MongoStaticInitializer(IOptions<MongoStaticOptions> options)
    {
        this._options = options;
    }

    public void Initialize()
    {
        if (_initialized)
        {
            return;
        }

        lock (_lockObject)
        {
            if (_initialized)
            {
                return;
            }

            // Guid representation V3 will be the default in Mongo C# driver 3.x so we use it already
            BsonDefaults.GuidRepresentationMode = GuidRepresentationMode.V3;

            foreach (var kvp in this._options.Value.BsonSerializers)
            {
                BsonSerializer.RegisterSerializer(kvp.Key, kvp.Value);
            }

            foreach (var conventionPack in this._options.Value.ConventionPacks)
            {
                ConventionRegistry.Remove(conventionPack.Name);
                ConventionRegistry.Register(conventionPack.Name, conventionPack, conventionPack.TypeFilter);
            }

            _initialized = true;
        }
    }
}