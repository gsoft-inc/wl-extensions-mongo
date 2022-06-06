using System.Reflection;

namespace GSoft.Infra.Mongo.Indexing;

public interface IMongoIndexer
{
    Task UpdateIndexesAsync(IEnumerable<Assembly> assemblies, CancellationToken cancellationToken = default);

    Task UpdateIndexesAsync(IEnumerable<Type> types, CancellationToken cancellationToken = default);
}