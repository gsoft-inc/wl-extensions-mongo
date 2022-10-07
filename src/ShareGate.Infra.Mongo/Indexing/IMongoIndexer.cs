using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace ShareGate.Infra.Mongo.Indexing;

public interface IMongoIndexer
{
    [SuppressMessage("ApiDesign", "RS0026:Do not add multiple public overloads with optional parameters", Justification = "That's a common pattern to use the default cancellation token")]
    Task UpdateIndexesAsync(Assembly assembly, string? clientName = null, string? databaseName = null, CancellationToken cancellationToken = default);

    [SuppressMessage("ApiDesign", "RS0026:Do not add multiple public overloads with optional parameters", Justification = "That's a common pattern to use the default cancellation token")]
    Task UpdateIndexesAsync(IEnumerable<Assembly> assemblies, string? clientName = null, string? databaseName = null, CancellationToken cancellationToken = default);

    [SuppressMessage("ApiDesign", "RS0026:Do not add multiple public overloads with optional parameters", Justification = "That's a common pattern to use the default cancellation token")]
    Task UpdateIndexesAsync(IEnumerable<Type> types, string? clientName = null, string? databaseName = null, CancellationToken cancellationToken = default);
}