using Microsoft.Extensions.Logging;
using MongoDB.Driver.Core.Events;

namespace Workleap.Extensions.Mongo.Performance;

internal sealed class CommandPerformanceEventSubscriber : AggregatorEventSubscriber, IDisposable
{
    private readonly IMongoClientProvider _mongoClientProvider;
    private readonly MongoCommandPerformanceAnalysisOptions _options;
    private readonly ILoggerFactory _loggerFactory;
    private readonly string _clientName;

    private CommandPerformanceAnalyzer? _performanceAnalyzer;

    public CommandPerformanceEventSubscriber(IMongoClientProvider mongoClientProvider, MongoCommandPerformanceAnalysisOptions options, ILoggerFactory loggerFactory, string clientName)
    {
        this._mongoClientProvider = mongoClientProvider;
        this._options = options;
        this._loggerFactory = loggerFactory;
        this._clientName = clientName;

        this.Subscribe<CommandStartedEvent>(this.CommandStartedEventHandler);
    }

    private void CommandStartedEventHandler(CommandStartedEvent evt)
    {
        if (!CommandPerformanceConstants.AllowedCommandsAndNames.ContainsKey(evt.CommandName))
        {
            return;
        }

        if (this._performanceAnalyzer == null)
        {
            // The performance analyzer cannot be built in the constructor as it depends on the mongo client that is not available yet at this point
            var mongoClient = this._mongoClientProvider.GetClient(this._clientName);

            this._performanceAnalyzer = new CommandPerformanceAnalyzer(mongoClient, this._options, this._loggerFactory);
            this._performanceAnalyzer.StartBackgroundTask();
        }

        this._performanceAnalyzer.AnalyzeCommand(new CommandToAnalyze(evt.DatabaseNamespace.DatabaseName, evt.RequestId, evt.CommandName, evt.Command));
    }

    public void Dispose()
    {
        this._performanceAnalyzer?.Dispose();
    }
}