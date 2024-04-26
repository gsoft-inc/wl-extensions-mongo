using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace Workleap.Extensions.Mongo.Indexing;

/// <summary>
/// Associates a concrete <see cref="IMongoDocument"/> class with its <see cref="MongoIndexProvider"/>.
/// </summary>
internal abstract class IndexRegistry : List<DocumentTypeEntry>
{
    protected IndexRegistry(IEnumerable<DocumentTypeEntry> documentTypeEntries)
    {
        foreach (var documentTypeEntry in documentTypeEntries)
        {
            var indexProviderType = documentTypeEntry.IndexProviderType;
            var documentType = documentTypeEntry.DocumentType;

            indexProviderType.EnsureHasPublicParameterlessConstructor();

            if (!IsIndexProvider(indexProviderType, out var indexProviderDocumentType))
            {
                throw new InvalidOperationException($"Type '{indexProviderType} must derive from '{typeof(MongoIndexProvider<>)}");
            }

            if (documentType != indexProviderDocumentType)
            {
                throw new InvalidOperationException($"Type '{indexProviderType} must provides index models for the document type '{documentType}'");
            }
            
            this.Add(documentTypeEntry);
        }
    }

    private static bool IsIndexProvider(Type? type, [MaybeNullWhen(false)] out Type documentType)
    {
        if (type == null || type.IsAbstract)
        {
            documentType = null;
            return false;
        }

        while (type != null && type != typeof(object))
        {
            var cur = type.IsGenericType ? type.GetGenericTypeDefinition() : type;
            if (cur == typeof(MongoIndexProvider<>))
            {
                documentType = type.GenericTypeArguments[0];
                return true;
            }

            type = type.BaseType;
        }

        documentType = null;
        return false;
    }
}

internal sealed class AttributeIndexRegistry : IndexRegistry
{
    public AttributeIndexRegistry(IEnumerable<Type> documentTypes)
        : base(documentTypes.Select(CreateDocumentTypeEntry))
    {
    }
    
    private static DocumentTypeEntry CreateDocumentTypeEntry(Type documentType)
    {
        if (!documentType.IsConcreteMongoDocumentType())
        {
            throw new ArgumentException($"Type '{documentType}' must implement {nameof(IMongoDocument)}");
        }

        var mongoCollectionAttribute = documentType.GetCustomAttribute<MongoCollectionAttribute>(inherit: false);
        if (mongoCollectionAttribute == null)
        {
            throw new InvalidOperationException($"Type '{documentType}' must be decorated with '{nameof(MongoCollectionAttribute)}'");
        }

        var indexProviderType = mongoCollectionAttribute.IndexProviderType ?? typeof(EmptyMongoIndexProvider<>).MakeGenericType(documentType);
        
        return new DocumentTypeEntry(documentType, indexProviderType);
    }
}

internal sealed class ConfigurationIndexRegistry : IndexRegistry
{
    public ConfigurationIndexRegistry(IEnumerable<Type> documentTypes, IReadOnlyDictionary<Type, Type> indexProviderTypes) 
        : base(documentTypes.Select(t => CreateDocumentTypeEntry(t, indexProviderTypes)))
    {
    }

    private static DocumentTypeEntry CreateDocumentTypeEntry(Type documentType, IReadOnlyDictionary<Type, Type> indexProviderTypes)
    {
        var indexProviderType = indexProviderTypes.TryGetValue(documentType, out var concreteIndexProviderType)
            ? concreteIndexProviderType 
            : typeof(EmptyMongoIndexProvider<>).MakeGenericType(documentType);

        return new DocumentTypeEntry(documentType, indexProviderType);
    }
}