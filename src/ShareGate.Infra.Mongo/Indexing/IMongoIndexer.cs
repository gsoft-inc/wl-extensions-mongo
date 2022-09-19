using System.Reflection;

namespace ShareGate.Infra.Mongo.Indexing;

public interface IMongoIndexer
{
    Task UpdateIndexesAsync(Assembly assembly, string? clientName = null, string? databaseName = null, CancellationToken cancellationToken = default);

    Task UpdateIndexesAsync(IEnumerable<Assembly> assemblies, string? clientName = null, string? databaseName = null, CancellationToken cancellationToken = default);

    Task UpdateIndexesAsync(IEnumerable<Type> types, string? clientName = null, string? databaseName = null, CancellationToken cancellationToken = default);
}