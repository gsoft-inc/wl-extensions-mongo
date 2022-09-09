#if NET6_0_OR_GREATER
using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Core.Events;

namespace ShareGate.Infra.Mongo;

internal sealed class CommandPerformanceEventSubscriber : AggregatorEventSubscriber, IDisposable
{
    private static readonly IReadOnlyDictionary<string, HashSet<string>> Foo = new Dictionary<string, HashSet<string>>
    {
        ["find"] = new HashSet<string>
        {
            "find", "filter", "sort", "projection", "hint", "skip", "limit", "batchSize", "singleBatch",
            "comment", "maxTimeMS", "readConcern", "max", "min", "returnKey", "showRecordId", "tailable",
            "oplogReplay", "noCursorTimeout", "awaitData", "allowPartialResults", "collation", "allowDiskUse", "let",
        },
    };

    private readonly IServiceProvider _serviceProvider;
    private ComamandPerformanceAnalyzer? _performanceAnalyzer;

    public CommandPerformanceEventSubscriber(IServiceProvider serviceProvider)
    {
        this._serviceProvider = serviceProvider;
        this.Subscribe<CommandStartedEvent>(this.CommandStartedEventHandler);
    }

    private void CommandStartedEventHandler(CommandStartedEvent evt)
    {
        if (Foo.ContainsKey(evt.CommandName))
        {
            if (this._performanceAnalyzer == null)
            {
                this._performanceAnalyzer = this._serviceProvider.GetRequiredService<ComamandPerformanceAnalyzer>();
                this._performanceAnalyzer.Start();
            }

            this._performanceAnalyzer.AddItem(evt.Command.ToJson());
        }
    }

    public void Dispose()
    {
        this._performanceAnalyzer?.Dispose();
    }
}

internal sealed class ComamandPerformanceAnalyzer : IDisposable
{
    private readonly IMongoDatabase _database;
    private readonly ILogger<ComamandPerformanceAnalyzer> _logger;
    private readonly ChannelReader<string> _channelReader;
    private readonly ChannelWriter<string> _channelWriter;

    public ComamandPerformanceAnalyzer(IMongoDatabase database, ILogger<ComamandPerformanceAnalyzer> logger)
    {
        this._database = database;
        this._logger = logger;

        var channel = Channel.CreateUnbounded<string>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = true,
        });

        this._channelReader = channel.Reader;
        this._channelWriter = channel.Writer;
    }

    public void AddItem(string item)
    {
        this._channelWriter.TryWrite(item);
    }

    public async void Start()
    {
        try
        {
            while (true)
            {
                var item = this._channelReader.ReadAsync();
            }
        }
        catch (ChannelClosedException)
        {
        }
    }

    public void Dispose()
    {
        this._channelWriter.Complete();
    }
}
#endif