using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver.Core.Events;

namespace ShareGate.Infra.Mongo.Logging;

internal sealed class CommandLoggingEventSubscriber : AggregatorEventSubscriber
{
    // These commands are automatically executed and add noise to the log output
    private static readonly HashSet<string> IgnoredCommandNames = new HashSet<string>(StringComparer.Ordinal)
    {
        "isMaster", "buildInfo", "saslStart", "saslContinue",
    };

    private readonly ILogger<CommandLoggingEventSubscriber> _logger;
    private readonly bool _enableSensitiveInformationLogging;

    public CommandLoggingEventSubscriber(ILoggerFactory loggerFactory, bool enableSensitiveInformationLogging)
    {
        this._logger = loggerFactory.CreateLogger<CommandLoggingEventSubscriber>();
        this._enableSensitiveInformationLogging = enableSensitiveInformationLogging;

        this.Subscribe<CommandStartedEvent>(this.CommandStartedEventHandler);
        this.Subscribe<CommandSucceededEvent>(this.CommandSucceededEventHandler);
        this.Subscribe<CommandFailedEvent>(this.CommandFailedEventHandler);
    }

    private void CommandStartedEventHandler(CommandStartedEvent evt)
    {
        if (IgnoredCommandNames.Contains(evt.CommandName))
        {
            return;
        }

        if (this._enableSensitiveInformationLogging)
        {
            this._logger.CommandStartedSensitive(evt.CommandName, evt.RequestId, evt.Command.ToJson());
        }
        else
        {
            this._logger.CommandStartedNonSensitive(evt.CommandName, evt.RequestId);
        }
    }

    private void CommandSucceededEventHandler(CommandSucceededEvent evt)
    {
        if (!IgnoredCommandNames.Contains(evt.CommandName))
        {
            this._logger.CommandSucceeded(evt.CommandName, evt.RequestId, evt.Duration.TotalSeconds);
        }
    }

    private void CommandFailedEventHandler(CommandFailedEvent evt)
    {
        // Manually cancelled MongoDB commands should not be logged as warnings.
        // The OperationCanceledException will eventually appear in the logs if not handled.
        var logLevel = evt.Failure is OperationCanceledException ? LogLevel.Debug : LogLevel.Warning;
        this._logger.CommandFailed(logLevel, evt.Failure, evt.CommandName, evt.RequestId, evt.Duration.TotalSeconds);
    }
}