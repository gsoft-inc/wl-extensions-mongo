using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using MongoDB.Driver.Core.Events;

namespace Workleap.Extensions.Mongo.Telemetry;

internal sealed class CommandLoggingEventSubscriber : AggregatorEventSubscriber
{
    private readonly MongoClientOptions _options;
    private readonly ILogger<CommandLoggingEventSubscriber> _logger;
    private readonly ConcurrentDictionary<int, Activity?> _currentActivitiesMap;

    public CommandLoggingEventSubscriber(MongoClientOptions options, ILoggerFactory loggerFactory)
    {
        this._options = options;
        this._logger = loggerFactory.CreateLogger<CommandLoggingEventSubscriber>();
        this._currentActivitiesMap = new ConcurrentDictionary<int, Activity?>();

        this.Subscribe<CommandStartedEvent>(this.CommandStartedEventHandler);
        this.Subscribe<CommandSucceededEvent>(this.CommandSucceededEventHandler);
        this.Subscribe<CommandFailedEvent>(this.CommandFailedEventHandler);
    }

    private void CommandStartedEventHandler(CommandStartedEvent evt)
    {
        if (this._options.Telemetry.IgnoredCommandNames.Contains(evt.CommandName))
        {
            return;
        }

        if (this._options.Telemetry.CaptureCommandText)
        {
            this._logger.CommandStartedSensitive(evt.CommandName, evt.RequestId, evt.Command.ToString());
        }
        else
        {
            this._logger.CommandStartedNonSensitive(evt.CommandName, evt.RequestId);
        }

        this._currentActivitiesMap.TryAdd(evt.RequestId, Activity.Current);
    }

    private void CommandSucceededEventHandler(CommandSucceededEvent evt)
    {
        if (this._currentActivitiesMap.TryRemove(evt.RequestId, out var originatingActivity))
        {
            TracingHelper.WithTemporaryCurrentActivity(originatingActivity, evt, this._logger, static (logger, evt) =>
            {
                logger.CommandSucceeded(evt.CommandName, evt.RequestId, evt.Duration.TotalSeconds);
            });
        }
    }

    private void CommandFailedEventHandler(CommandFailedEvent evt)
    {
        if (this._currentActivitiesMap.TryRemove(evt.RequestId, out var originatingActivity))
        {
            TracingHelper.WithTemporaryCurrentActivity(originatingActivity, evt, this._logger, static (logger, evt) =>
            {
                // Manually-cancelled MongoDB commands should not be logged as warnings.
                // The OperationCanceledException will eventually appear in the logs if not handled.
                var logLevel = evt.Failure is OperationCanceledException ? LogLevel.Debug : LogLevel.Warning;
                logger.CommandFailed(logLevel, evt.Failure, evt.CommandName, evt.RequestId, evt.Duration.TotalSeconds);
            });
        }
    }
}