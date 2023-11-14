﻿using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace Workleap.Extensions.Mongo.Indexing;

/// <summary>
/// Associates a concrete <see cref="IMongoDocument"/> class with its <see cref="MongoIndexProvider"/>.
/// </summary>
internal sealed class IndexRegistry : List<DocumentTypeEntry>
{
    public IndexRegistry(IEnumerable<Type> documentTypes)
    {
        foreach (var documentType in documentTypes)
        {
            if (!MongoReflectionCache.IsConcreteMongoDocumentType(documentType))
            {
                throw new ArgumentException($"Type '{documentType}' must implement {nameof(IMongoDocument)}");
            }

            var mongoCollectionAttribute = documentType.GetCustomAttribute<MongoCollectionAttribute>(inherit: false);
            if (mongoCollectionAttribute == null)
            {
                throw new InvalidOperationException($"Type '{documentType}' must be decorated with '{nameof(MongoCollectionAttribute)}'");
            }

            var indexProviderType = mongoCollectionAttribute.IndexProviderType ?? typeof(EmptyMongoIndexProvider<>).MakeGenericType(documentType);

            if (!HasPublicParameterlessConstructor(indexProviderType))
            {
                throw new InvalidOperationException($"Type {indexProviderType}' must have a public parameterless constructor");
            }

            if (!IsIndexProvider(indexProviderType, out var indexProviderDocumentType))
            {
                throw new InvalidOperationException($"Type '{indexProviderType} must derive from '{typeof(MongoIndexProvider<>)}");
            }

            if (documentType == indexProviderDocumentType)
            {
                this.Add(new DocumentTypeEntry(documentType, indexProviderType));
            }
            else
            {
                throw new InvalidOperationException($"Type '{indexProviderType} must provides index models for the document type '{documentType}'");
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