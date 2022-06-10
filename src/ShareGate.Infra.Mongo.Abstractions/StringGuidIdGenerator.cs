using MongoDB.Bson.Serialization;

namespace ShareGate.Infra.Mongo;

// Inspired from
// https://github.com/mongodb/mongo-csharp-driver/blob/eeafbea0921243a5868b81984e1083a07c1f75bc/src/MongoDB.Bson/Serialization/IdGenerators/GuidGenerator.cs
// https://github.com/mongodb/mongo-csharp-driver/blob/eeafbea0921243a5868b81984e1083a07c1f75bc/src/MongoDB.Bson/Serialization/IdGenerators/StringObjectIdGenerator.cs
public sealed class StringGuidIdGenerator : IIdGenerator
{
    public static IIdGenerator Instance { get; } = new StringGuidIdGenerator();

    public object GenerateId(object container, object document)
    {
        return GenerateId();
    }

    public static object GenerateId()
    {
        return Guid.NewGuid().ToString("D");
    }

    public bool IsEmpty(object id)
    {
        return string.IsNullOrEmpty((string)id);
    }
}