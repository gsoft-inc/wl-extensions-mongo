using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Conventions;

namespace GSoft.Extensions.Mongo;

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

            BsonDefaults.GuidRepresentationMode = this._options.Value.GuidRepresentationMode;

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