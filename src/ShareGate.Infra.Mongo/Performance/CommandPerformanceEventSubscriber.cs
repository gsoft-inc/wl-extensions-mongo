using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MongoDB.Driver.Core.Events;

namespace ShareGate.Infra.Mongo.Performance;

internal sealed class CommandPerformanceEventSubscriber : AggregatorEventSubscriber, IDisposable
{
    private readonly IServiceProvider _serviceProvider;
    private CommandPerformanceAnalyzer? _performanceAnalyzer;

    public CommandPerformanceEventSubscriber(IServiceProvider serviceProvider, IOptions<MongoOptions> options)
    {
        this._serviceProvider = serviceProvider;

        if (options.Value.CommandPerformanceAnalysis.IsPerformanceAnalysisEnabled)
        {
            this.Subscribe<CommandStartedEvent>(this.CommandStartedEventHandler);
        }
    }

    private void CommandStartedEventHandler(CommandStartedEvent evt)
    {
        if (CommandPerformanceConstants.AllowedCommandsAndNames.ContainsKey(evt.CommandName))
        {
            if (this._performanceAnalyzer == null)
            {
                // The performance analyzer is lazy-instanciated from IServiceProvider on purpose
                // It cannot be injected in the constructor as it depends on the database that is not yet available at this point
                this._performanceAnalyzer = this._serviceProvider.GetRequiredService<CommandPerformanceAnalyzer>();
                this._performanceAnalyzer.Start();
            }

            this._performanceAnalyzer.AnalyzeCommand(new CommandToAnalyze(evt.CommandName, evt.Command, evt.RequestId));
        }
    }

    public void Dispose()
    {
        this._performanceAnalyzer?.Dispose();
    }
}