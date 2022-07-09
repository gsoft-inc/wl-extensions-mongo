using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;

namespace ShareGate.Infra.Mongo.Ephemeral;

public static class EphemeralMongoBuilderExtensions
{
    /// <summary>
    /// Provides a real implementation of a MongoDB cluster using an ephemeral localhost server that will be destroyed
    /// when it will no longer be used by any running test. The first startup can take approximately two seconds on a dev workstation.
    /// https://github.com/Mongo2Go/Mongo2Go
    /// </summary>
    public static MongoBuilder UseEphemeralRealServer(this MongoBuilder builder)
    {
        builder.Services.AddSingleton<DefaultDatabaseNameHolder>();
        builder.Services.AddSingleton<ReusableMongoDbRunner>();
        builder.Services.ConfigureOptions<EphemeralMongoSetup>();

        var existingMongoClientDescriptors = new List<(int, ServiceDescriptor)>();

        // Find existing IMongoClient service descriptors with their indexes in the services list
        for (var index = 0; index < builder.Services.Count; index++)
        {
            var descriptor = builder.Services[index];
            if (descriptor.ServiceType != typeof(IMongoClient))
            {
                continue;
            }

            if (descriptor.Lifetime != ServiceLifetime.Singleton)
            {
                throw new NotSupportedException("IMongoClient service descriptor lifetime must be singleton");
            }

            existingMongoClientDescriptors.Add((index, descriptor));
        }

        if (existingMongoClientDescriptors.Count == 0)
        {
            throw new InvalidOperationException("At least one IMongoClient service descriptor is required");
        }

        // Wrap each existing IMongoClient service descriptor with a new one that drops its default database when disposed
        foreach (var (index, existingMongoClientDescriptor) in existingMongoClientDescriptors)
        {
            var newMongoClientDescriptor = ServiceDescriptor.Singleton(serviceProvider => CreateDisposableMongoClient(serviceProvider, existingMongoClientDescriptor));
            builder.Services.Insert(index, newMongoClientDescriptor);
            builder.Services.Remove(existingMongoClientDescriptor);
        }

        return builder;
    }

    private static IMongoClient CreateDisposableMongoClient(IServiceProvider serviceProvider, ServiceDescriptor existingMongoClientDescriptor)
    {
        IMongoClient existingMongoClient;

        if (existingMongoClientDescriptor.ImplementationInstance != null)
        {
            existingMongoClient = (IMongoClient)existingMongoClientDescriptor.ImplementationInstance;
        }
        else if (existingMongoClientDescriptor.ImplementationFactory != null)
        {
            existingMongoClient = (IMongoClient)existingMongoClientDescriptor.ImplementationFactory(serviceProvider);
        }
        else if (existingMongoClientDescriptor.ImplementationType != null)
        {
            existingMongoClient = (IMongoClient)ActivatorUtilities.GetServiceOrCreateInstance(serviceProvider, existingMongoClientDescriptor.ImplementationType);
        }
        else
        {
            throw new InvalidOperationException("IMongoClient service descriptor must provide an implementation");
        }

        var databaseNameHolder = serviceProvider.GetRequiredService<DefaultDatabaseNameHolder>();
        return new DisposableMongoClient(existingMongoClient, databaseNameHolder);
    }
}