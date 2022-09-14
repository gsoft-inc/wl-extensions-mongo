using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace ShareGate.Infra.Mongo;

internal sealed class MongoClientProvider : IMongoClientProvider
{
    private readonly IServiceProvider _serviceProvider;

    // MongoDB C# driver documentation says that IMongoClient, IMongoDatabase and IMongoCollection<> are thread-safe and can be stored globally (i.e. as singletons):
    // https://mongodb.github.io/mongo-csharp-driver/2.10/reference/driver/connecting/
    private readonly ConcurrentDictionary<string, Lazy<IMongoClient>> _mongoClients;

    public MongoClientProvider(IServiceProvider serviceProvider)
    {
        this._serviceProvider = serviceProvider;
        this._mongoClients = new ConcurrentDictionary<string, Lazy<IMongoClient>>(StringComparer.Ordinal);
    }

    public IMongoClient GetClient(string clientName)
    {
        if (clientName == null)
        {
            throw new ArgumentNullException(nameof(clientName));
        }

        return this._mongoClients.GetOrAdd(clientName, this.LazyMongoClientFactory).Value;
    }

    private Lazy<IMongoClient> LazyMongoClientFactory(string clientName)
    {
        return new Lazy<IMongoClient>(() => this.MongoClientFactory(clientName));
    }

    private IMongoClient MongoClientFactory(string clientName)
    {
        this._serviceProvider.GetRequiredService<MongoInitializer>().Initialize();

        var options = this._serviceProvider.GetRequiredService<IOptionsMonitor<MongoClientOptions>>().Get(clientName);
        var settings = MongoClientSettings.FromConnectionString(options.ConnectionString);

        // Default socket timeout is infinite. 60 seconds is the timeout used by OV.
        // Keeping infinite timeout means we could wait up to 15 minutes or even more (as seen in SG Cloud Copy)
        if (settings.SocketTimeout == TimeSpan.Zero)
        {
            settings.SocketTimeout = TimeSpan.FromSeconds(60);
        }

        // Default connect timeout is 30 seconds. 10 seconds is the timeout used by OV
        if (settings.ConnectTimeout == TimeSpan.FromSeconds(30))
        {
            settings.ConnectTimeout = TimeSpan.FromSeconds(10);
        }

        var eventSubscriberFactories = this._serviceProvider.GetServices<IMongoEventSubscriberFactory>();
        var eventSubscribers = eventSubscriberFactories.SelectMany(x => x.CreateEventSubscribers(clientName));

        settings.ClusterConfigurator = builder =>
        {
            foreach (var eventSubscriber in eventSubscribers)
            {
                builder.Subscribe(eventSubscriber);
            }
        };

        // Allow consumers from overriding mongo client settings
        options.MongoClientSettingsConfigurator?.Invoke(settings);

        return new MongoClient(settings);
    }
}