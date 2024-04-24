using System.Reflection;

namespace Workleap.Extensions.Mongo.Indexing;

internal sealed class AttributeIndexDetectionStrategy : IIndexDetectionStrategy
{
    public IReadOnlyList<Type> GetDocumentTypes(IEnumerable<Type> allTypes)
    {
        return allTypes.Where(IsDocumentTypesWithExplicitMongoCollectionAttribute)
            .ToArray();
    }

    public void ValidateType(Type type)
    {
        if (!type.IsConcreteMongoDocumentType())
        {
            throw new ArgumentException($"Type '{type}' must implement {nameof(IMongoDocument)}");
        }
    }

    public IndexRegistry CreateRegistry(IEnumerable<Type> documentTypes)
    {
        return new AttributeIndexRegistry(documentTypes);
    }

    internal static bool IsDocumentTypesWithExplicitMongoCollectionAttribute(Type typeCandidate)
    {
        return typeCandidate.IsConcreteMongoDocumentType() && typeCandidate.GetCustomAttribute<MongoCollectionAttribute>(inherit: false) != null;
    }
}