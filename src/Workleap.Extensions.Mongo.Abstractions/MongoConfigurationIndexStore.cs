using System.Collections.Concurrent;

namespace Workleap.Extensions.Mongo;

internal static class MongoConfigurationIndexStore
{
    private static readonly ConcurrentDictionary<Type, Type?> IndexProviderTypes = new();

    internal static void AddIndexProviderType(Type documentType, Type? indexProviderType)
    {
        if (!IndexProviderTypes.TryAdd(documentType, indexProviderType))
        {
            throw new ArgumentException($"IndexProviderType for {documentType} already set.");
        }
    }

    internal static Type? GetIndexProviderType(Type documentType) => IndexProviderTypes[documentType];

    internal static IEnumerable<Type> GetRegisteredDocumentTypes() => IndexProviderTypes.Keys;
}