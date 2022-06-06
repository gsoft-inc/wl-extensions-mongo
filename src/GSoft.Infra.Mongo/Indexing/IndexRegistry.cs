using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace GSoft.Infra.Mongo.Indexing;

/// <summary>
/// Associates a concrete <see cref="IMongoDocument"/> class with its single <see cref="MongoIndexProvider"/>.
/// </summary>
internal sealed class IndexRegistry : Dictionary<Type, Type>
{
    public IndexRegistry(IEnumerable<Type> types)
    {
        foreach (var type in types)
        {
            if (!MongoReflectionCache.IsConcreteMongoDocumentType(type))
            {
                throw new ArgumentException($"Type '{type}' must implement {nameof(IMongoDocument)}");
            }

            var indexProviderType = type.GetCustomAttribute<MongoCollectionAttribute>()?.IndexProviderType ?? typeof(EmptyMongoIndexProvider<>).MakeGenericType(type);

            if (!HasPublicParameterlessConstructor(indexProviderType))
            {
                throw new InvalidOperationException($"Type {indexProviderType}' must have a public parameterless constructor");
            }

            if (!IsIndexProvider(indexProviderType, out var documentType))
            {
                throw new InvalidOperationException($"Type '{indexProviderType} must derive from '{typeof(MongoIndexProvider<>)}");
            }

            if (type == documentType)
            {
                this.Add(type, indexProviderType);
            }
            else
            {
                throw new InvalidOperationException($"Type '{indexProviderType} must use '{type}' as its generic argument");
            }
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

    private static bool HasPublicParameterlessConstructor(Type type)
    {
        return type.GetConstructor(BindingFlags.Public | BindingFlags.Instance, binder: null, Type.EmptyTypes, modifiers: null) != null;
    }
}