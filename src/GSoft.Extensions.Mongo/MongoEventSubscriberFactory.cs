using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Driver.Core.Events;
using GSoft.Extensions.Mongo.Performance;
using GSoft.Extensions.Mongo.Telemetry;

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

        // Command distributed tracing (Open Telemetry)
        yield return new CommandTracingEventSubscriber(options);

        // Command logging
        yield return new CommandLoggingEventSubscriber(options, this._loggerFactory);

        // Command performance analysis
        if (options.CommandPerformanceAnalysis.IsPerformanceAnalysisEnabled)
        {
            yield return new CommandPerformanceEventSubscriber(this._mongoClientProvider, options.CommandPerformanceAnalysis, this._loggerFactory, clientName);
        }
    }
}