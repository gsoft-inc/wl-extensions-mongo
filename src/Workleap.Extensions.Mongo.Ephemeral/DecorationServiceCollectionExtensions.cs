using Microsoft.Extensions.DependencyInjection;

namespace Workleap.Extensions.Mongo.Ephemeral;

// Inspired from Scrutor: https://github.com/khellang/Scrutor/blob/v4.0.0/src/Scrutor/ServiceCollectionExtensions.Decoration.cs
// Modifying IServiceCollection registrations can be dangerous and not aligned with the Microsoft vision of dependency injection
// That's why these methods only exist in test projects where we want to wrap things in order to do some assertions
internal static class DecorationServiceCollectionExtensions
{
    public static IServiceCollection DecorateWithSameLifetime<TService>(this IServiceCollection services, Func<IServiceProvider, TService, TService> decoratorFactory)
        where TService : class
    {
        if (!services.TryDecorateDescriptors(typeof(TService), x => x.Decorate(decoratorFactory)))
        {
            throw new Exception($"Could not find a registration of type {typeof(TService)} in the service collection");
        }

        return services;
    }

    private static bool TryDecorateDescriptors(this IServiceCollection services, Type serviceType, Func<ServiceDescriptor, ServiceDescriptor> decorator)
    {
        var descriptors = services.FindDescriptors(serviceType).ToArray();

        foreach (var (index, descriptor) in descriptors)
        {
            services.Insert(index, decorator(descriptor));
            services.Remove(descriptor);
        }

        return descriptors.Length > 0;
    }

    private static IEnumerable<(int Index, ServiceDescriptor Descriptor)> FindDescriptors(this IServiceCollection services, Type serviceType)
    {
        for (var index = 0; index < services.Count; ++index)
        {
            var descriptor = services[index];
            if (descriptor.ServiceType == serviceType)
            {
                yield return (index, descriptor);
            }
        }
    }

    private static ServiceDescriptor Decorate<TService, TDecorator>(this ServiceDescriptor descriptor, Func<IServiceProvider, TService, TDecorator> decoratorFactory)
        where TDecorator : class, TService
    {
        object ImplementationFactory(IServiceProvider serviceProvider)
        {
            var decoratedInstance = serviceProvider.GetInstance<TService>(descriptor);
            return decoratorFactory(serviceProvider, decoratedInstance);
        }

        return ServiceDescriptor.Describe(
            serviceType: descriptor.ServiceType,
            implementationFactory: ImplementationFactory,
            lifetime: descriptor.Lifetime);
    }

    private static TService GetInstance<TService>(this IServiceProvider serviceProvider, ServiceDescriptor descriptor)
    {
        if (descriptor.ImplementationInstance != null)
        {
            return (TService)descriptor.ImplementationInstance;
        }

        if (descriptor.ImplementationFactory != null)
        {
            return (TService)descriptor.ImplementationFactory(serviceProvider);
        }

        var implementationType = descriptor.ImplementationType;
        if (implementationType != null)
        {
            if (implementationType == descriptor.ServiceType)
            {
                // Since implementationType is equal to ServiceType we need explicitly create an implementation type through reflections in order to avoid infinite recursion.
                // Should not cause issue with singletons, since singleton will be a decorator and after this fact we can don't care about lifecycle of decorable service (for sure, if IDisposable of decorator disposes underlying type:))
                return (TService)ActivatorUtilities.CreateInstance(serviceProvider, implementationType);
            }

            return (TService)ActivatorUtilities.GetServiceOrCreateInstance(serviceProvider, implementationType);
        }

        throw new InvalidOperationException($"No implementation factory or instance or type found for {descriptor.ServiceType}.");
    }
}