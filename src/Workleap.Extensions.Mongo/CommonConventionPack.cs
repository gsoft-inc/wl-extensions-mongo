using MongoDB.Bson;
using MongoDB.Bson.Serialization.Conventions;

namespace Workleap.Extensions.Mongo;

internal class CommonConventionPack : NamedConventionPack
{
    public CommonConventionPack()
    {
        this.Add(new IgnoreExtraElementsConvention(ignoreExtraElements: true));
        this.Add(new EnumRepresentationConvention(BsonType.String));
    }

    public override string Name => nameof(CommonConventionPack);

    public override bool TypeFilter(Type type)
    {
        return true;
    }
}