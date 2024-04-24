using System.Reflection;

namespace Workleap.Extensions.Mongo;

public interface IMongoCollectionConfigurationBootstrapper
{
    void ApplyConfigurations(params Assembly[] assemblies);
}