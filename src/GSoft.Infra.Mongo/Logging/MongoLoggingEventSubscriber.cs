using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver.Core.Events;

// The class is in its own namespace so its logging can be easily filtered using logging configuration
namespace GSoft.Infra.Mongo.Logging;

internal sealed class MongoLoggingEventSubscriber : AggregatorEventSubscriber
{
    // These commands are automatically executed and add noise to the log output
    private static readonly HashSet<string> IgnoredCommandNames = new HashSet<string>(StringComparer.Ordinal)
    {
        "isMaster", "buildInfo", "saslStart", "saslContinue",
    };

    private readonly ILogger<MongoLoggingEventSubscriber> _logger;
    private readonly bool _enableSensitiveInformationLogging;

    public MongoLoggingEventSubscriber(ILogger<MongoLoggingEventSubscriber> logger, IOptions<MongoOptions> options)
    {
        this._logger = logger;
        this._enableSensitiveInformationLogging = options.Value.EnableSensitiveInformationLogging;

        this.Subscribe<CommandStartedEvent>(this.CommandStartedEventHandler);
        this.Subscribe<CommandSucceededEvent>(this.CommandSucceededEventHandler);
        this.Subscribe<CommandFailedEvent>(this.CommandFailedEventHandler);
    }

    private void CommandStartedEventHandler(CommandStartedEvent evt)
    {
        if (!IgnoredCommandNames.Contains(evt.CommandName))
        {
            if (this._enableSensitiveInformationLogging)
            {
                this._logger.LogDebug("Executing mongo command {MongoCommandName}:{MongoRequestId} {MongoCommandJson}", evt.CommandName, evt.RequestId, evt.Command.ToJson());
            }
            else
            {
                this._logger.LogDebug("Executing mongo command {MongoCommandName}:{MongoRequestId}", evt.CommandName, evt.RequestId);
            }
        }
    }

    private void CommandSucceededEventHandler(CommandSucceededEvent evt)
    {
        if (!IgnoredCommandNames.Contains(evt.CommandName))
        {
            this._logger.LogDebug("Successfully executed mongo command {MongoCommandName}:{MongoRequestId} in {MongoCommandDuration} seconds", evt.CommandName, evt.RequestId, evt.Duration.TotalSeconds);
        }
    }

    private void CommandFailedEventHandler(CommandFailedEvent evt)
    {
        // Manually cancelled MongoDB commands should not be logged as warnings.
        // The OperationCanceledException will eventually appear in the logs if not handled.
        var logLevel = evt.Failure is OperationCanceledException ? LogLevel.Debug : LogLevel.Warning;
        this._logger.Log(logLevel, evt.Failure, "Failed to execute mongo command {MongoCommandName}:{MongoRequestId} in {MongoCommandDuration} seconds", evt.CommandName, evt.RequestId, evt.Duration.TotalSeconds);
    }
}