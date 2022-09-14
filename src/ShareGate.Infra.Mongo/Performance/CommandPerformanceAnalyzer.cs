using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;

namespace ShareGate.Infra.Mongo.Performance;

internal sealed class CommandPerformanceAnalyzer : IDisposable
{
    private readonly IMongoDatabase _database;
    private readonly ILogger<CommandPerformanceAnalyzer> _logger;
    private readonly ChannelReader<CommandToAnalyze> _commandChannelReader;
    private readonly ChannelWriter<CommandToAnalyze> _commandChannelWriter;
    private readonly CancellationTokenSource _cancellationToken;
    private readonly bool _enableCollectionScanDetection;

    // A background task and a channel are used to analyze MongoDB commands without consuming a whole thread
    // The same process is used in the Azure.Extensions.AspNetCore.Configuration.Secrets package to periodically refresh secrets from Azure Key vault
    // https://github.com/Azure/azure-sdk-for-net/blob/Azure.Extensions.AspNetCore.Configuration.Secrets_1.2.2/sdk/extensions/Azure.Extensions.AspNetCore.Configuration.Secrets/src/AzureKeyVaultConfigurationProvider.cs
    private Task? _explainTask;
    private int _isDisposed;

    public CommandPerformanceAnalyzer(IMongoDatabase database, IOptions<MongoOptions> options, ILogger<CommandPerformanceAnalyzer> logger)
    {
        this._database = database;
        this._logger = logger;

        var commandChannel = Channel.CreateUnbounded<CommandToAnalyze>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = true,
        });

        this._commandChannelReader = commandChannel.Reader;
        this._commandChannelWriter = commandChannel.Writer;

        this._cancellationToken = new CancellationTokenSource();

        this._enableCollectionScanDetection = options.Value.CommandPerformanceAnalysis.EnableCollectionScanDetection;
    }

    public void AnalyzeCommand(CommandToAnalyze command)
    {
        if (Interlocked.CompareExchange(ref this._isDisposed, 1, 1) == 1)
        {
            throw new ObjectDisposedException(nameof(CommandPerformanceAnalyzer));
        }

        // The only reason why writing could fail is because the channel is already closed, so that's fine
        _ = this._commandChannelWriter.TryWrite(command);
    }

    public void Start()
    {
        if (Interlocked.CompareExchange(ref this._isDisposed, 1, 1) == 1)
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
        var explainCommand = BuildExplainCommand(command);

        try
        {
            var explainResult = await this._database.RunCommandAsync(explainCommand, cancellationToken: this._cancellationToken.Token).ConfigureAwait(false);

            if (this._enableCollectionScanDetection && explainResult.ExecutionStats.ExecutionStages.Stage == "COLLSCAN")
            {
                this._logger.CollectionScanDetected(command.RequestId);
            }
        }
        catch
        {
            // Ignored, if there's an issue with MongoDB it will be logged with the other event subscriber
        }
    }

    private static Command<ExplainResultDocument> BuildExplainCommand(CommandToAnalyze command)
    {
        var allowedNames = CommandPerformanceConstants.AllowedCommandsAndNames[command.CommandName];
        var sanitizedCommand = new BsonDocument();

        foreach (var name in command.Command.Names)
        {
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
        if (Interlocked.CompareExchange(ref this._isDisposed, 1, 0) == 0)
        {
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