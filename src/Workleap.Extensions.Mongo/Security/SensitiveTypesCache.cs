using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Workleap.ComponentModel.DataAnnotations;

namespace Workleap.Extensions.Mongo.Security;

internal static class SensitiveTypesCache
{
    private static readonly ConcurrentDictionary<Type, bool> Cache = new ConcurrentDictionary<Type, bool>();

    public static bool TryGetSensitiveInformation(MemberInfo property, [MaybeNullWhen(false)] out SensitiveInformationAttribute attribute)
    {
        attribute = property.MemberType == MemberTypes.Property ? property.GetCustomAttribute<SensitiveInformationAttribute>(inherit: true) : null;
        return attribute != null;
    }

    private static bool IsSensitiveProperty(MemberInfo property)
    {
        return TryGetSensitiveInformation(property, out _);
    }

    public static bool HasSensitiveProperty(Type type)
    {
        return Cache.GetOrAdd(type, HasSensitivePropertyInternal);
    }

    private static bool HasSensitivePropertyInternal(Type type)
    {
        if (IsBuiltInSystemType(type))
        {
            return false;
        }

        foreach (var property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            if (IsSensitiveProperty(property))
            {
                return true;
            }

            if (TryGetEnumerableGenericType(property.PropertyType, out var enumerableGenericType) && HasSensitiveProperty(enumerableGenericType))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsBuiltInSystemType(Type type)
    {
        var ns = type.Namespace;
        if (ns == null)
        {
            return false;
        }

        return ns.StartsWith("System", StringComparison.Ordinal)
            || ns.StartsWith("Microsoft", StringComparison.Ordinal)
            || ns.StartsWith("MongoDB", StringComparison.Ordinal);
    }

    private static bool TryGetEnumerableGenericType(Type type, [MaybeNullWhen(false)] out Type genericType)
    {
        var enumerableType = type.GetInterfaces().Append(type).FirstOrDefault(x => x.IsGenericType && x.GetGenericTypeDefinition() == typeof(IEnumerable<>));

        if (enumerableType == null)
        {
            genericType = null;
            return false;
        }

        genericType = enumerableType.GetGenericArguments()[0];
        return true;
    }
}