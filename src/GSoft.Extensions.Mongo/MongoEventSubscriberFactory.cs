using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Driver.Core.Events;
using GSoft.Extensions.Mongo.Logging;
using GSoft.Extensions.Mongo.Performance;

namespace GSoft.Extensions.Mongo;

internal sealed class MongoEventSubscriberFactory : IMongoEventSubscriberFactory
{
    private readonly IMongoClientProvider _mongoClientProvider;
    private readonly IOptionsMonitor<MongoClientOptions> _optionsMonitor;
    private readonly ILoggerFactory _loggerFactory;

    public MongoEventSubscriberFactory(IMongoClientProvider mongoClientProvider, IOptionsMonitor<MongoClientOptions> optionsMonitor, ILoggerFactory loggerFactory)
    {
        this._mongoClientProvider = mongoClientProvider;
        this._optionsMonitor = optionsMonitor;
        this._loggerFactory = loggerFactory;
    }

    public IEnumerable<IEventSubscriber> CreateEventSubscribers(string clientName)
    {
        var options = this._optionsMonitor.Get(clientName);

        // Command logging
        yield return new CommandLoggingEventSubscriber(this._loggerFactory, options.EnableSensitiveInformationLogging);

        // Command performance analysis
        if (options.CommandPerformanceAnalysis.IsPerformanceAnalysisEnabled)
        {
            yield return new CommandPerformanceEventSubscriber(this._mongoClientProvider, options.CommandPerformanceAnalysis, this._loggerFactory, clientName);
        }
    }
}