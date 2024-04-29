using System.Reflection;

namespace Workleap.Extensions.Mongo;

internal static class MongoTypeExtensions
{
    internal static bool IsConcreteMongoDocumentType(this Type type) => !type.IsAbstract && typeof(IMongoDocument).IsAssignableFrom(type);
    
    internal static bool IsMongoCollectionConfigurationInterface(this Type t) => t.IsGenericType && t.GetGenericTypeDefinition() == typeof(IMongoCollectionConfiguration<>);
    
    internal static bool HasPublicParameterlessConstructor(this Type type) => type.GetConstructor(BindingFlags.Public | BindingFlags.Instance, binder: null, Type.EmptyTypes, modifiers: null) != null;

    internal static void EnsureHasPublicParameterlessConstructor(this Type type)
    {
        if (!type.HasPublicParameterlessConstructor())
        {
            throw new InvalidOperationException($"Type {type}' must have a public parameterless constructor");
        }
    }
}