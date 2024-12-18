using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;

namespace Workleap.Extensions.Mongo.Performance;

internal sealed class CommandPerformanceAnalyzer : IDisposable
{
    private readonly IMongoClient _mongoClient;
    private readonly ILogger<CommandPerformanceAnalyzer> _logger;
    private readonly ChannelReader<CommandToAnalyze> _commandChannelReader;
    private readonly ChannelWriter<CommandToAnalyze> _commandChannelWriter;
    private readonly CancellationTokenSource _cancellationToken;
    private readonly bool _enableCollectionScanDetection;

    // A background task and a channel are used to analyze MongoDB commands without consuming a whole thread
    // The similar process is used in the Azure.Extensions.AspNetCore.Configuration.Secrets package to periodically refresh secrets from Azure Key vault
    // https://github.com/Azure/azure-sdk-for-net/blob/Azure.Extensions.AspNetCore.Configuration.Secrets_1.2.2/sdk/extensions/Azure.Extensions.AspNetCore.Configuration.Secrets/src/AzureKeyVaultConfigurationProvider.cs
    private Task? _explainTask;
    private bool _isDisposed;

    public CommandPerformanceAnalyzer(IMongoClient mongoClient, MongoCommandPerformanceAnalysisOptions options, ILoggerFactory loggerFactory)
    {
        this._mongoClient = mongoClient;
        this._logger = loggerFactory.CreateLogger<CommandPerformanceAnalyzer>();

        var commandChannel = Channel.CreateUnbounded<CommandToAnalyze>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = true,
        });

        this._commandChannelReader = commandChannel.Reader;
        this._commandChannelWriter = commandChannel.Writer;
        this._cancellationToken = new CancellationTokenSource();
        this._enableCollectionScanDetection = options.EnableCollectionScanDetection;
    }

    public void AnalyzeCommand(CommandToAnalyze command)
    {
#pragma warning disable CA1513 // Justification: Ensuring maintainability by using a common approach that supports multiple .NET frameworks        
        if (this._isDisposed)
        {
            throw new ObjectDisposedException(nameof(CommandPerformanceAnalyzer));
        }

        // The only reason why writing could fail is because the channel is already closed, so that's fine
        _ = this._commandChannelWriter.TryWrite(command);
    }

    public void StartBackgroundTask()
    {
#pragma warning disable CA1513 // Justification: Ensuring maintainability by using a common approach that supports multiple .NET frameworks
        if (this._isDisposed)
        {
            throw new ObjectDisposedException(nameof(CommandPerformanceAnalyzer));
        }

        this._explainTask ??= this.StartExplainsTask();
    }

    private async Task StartExplainsTask()
    {
        while (!this._cancellationToken.IsCancellationRequested)
        {
            try
            {
                var command = await this._commandChannelReader.ReadAsync().ConfigureAwait(false);
                _ = this.ExplainAsync(command);
            }
            catch (ChannelClosedException)
            {
                // Ignored, the app is probably shutting down
            }
        }
    }

    private async Task ExplainAsync(CommandToAnalyze command)
    {
        try
        {
            var database = this._mongoClient.GetDatabase(command.DatabaseName);
            var explainCommand = BuildExplainCommand(command);

            ExplainResultDocument explainResult;

            try
            {
                explainResult = await database.RunCommandAsync(explainCommand, cancellationToken: this._cancellationToken.Token).ConfigureAwait(false);
            }
            catch
            {
                // Ignored, if there's an issue with MongoDB it will be logged with the other event subscriber
                return;
            }

            if (this._enableCollectionScanDetection && explainResult.ExecutionStats.ExecutionStages.Stage == "COLLSCAN")
            {
                this._logger.CollectionScanDetected(command.RequestId);
            }
        }
        finally
        {
            // Command could be a BsonDocument derivative that implements IDisposable, and we want to avoid memory leaks
            if (command is { Command: IDisposable disposableCommand })
            {
                disposableCommand.Dispose();
            }
        }
    }

    private static Command<ExplainResultDocument> BuildExplainCommand(CommandToAnalyze command)
    {
        var allowedNames = CommandPerformanceConstants.AllowedCommandsAndNames[command.CommandName];
        var sanitizedCommand = new BsonDocument();

        foreach (var name in command.Command.Names)
        {
            // Mongo C# driver always adds extra BSON fields to each request, such as: $clusterTime, $db, lsid...
            // These cannot be sent to the explain command, as it will throw an error.
            // We can only send fields related to the specific request being executed.
            // https://www.mongodb.com/docs/manual/reference/command/explain/
            if (allowedNames.Contains(name))
            {
                sanitizedCommand[name] = command.Command[name];
            }
        }

        var explainCommand = new BsonDocument
        {
            ["explain"] = sanitizedCommand,
            ["verbosity"] = "executionStats",
        };

        return new BsonDocumentCommand<ExplainResultDocument>(explainCommand);
    }

    public void Dispose()
    {
        if (!this._isDisposed)
        {
            this._isDisposed = true;

            this._commandChannelWriter.Complete();
            this._cancellationToken.Cancel();
            this._cancellationToken.Dispose();
        }
    }

    private sealed class ExplainResultDocument
    {
        [BsonElement("executionStats")]
        public ExecutionStatsDocument ExecutionStats { get; set; } = new ExecutionStatsDocument();
    }

    private sealed class ExecutionStatsDocument
    {
        [BsonElement("executionStages")]
        public ExecutionStagesDocument ExecutionStages { get; set; } = new ExecutionStagesDocument();
    }

    private sealed class ExecutionStagesDocument
    {
        [BsonElement("stage")]
        public string Stage { get; set; } = string.Empty;
    }
}