using System.Collections.Concurrent;

namespace Workleap.Extensions.Mongo.Ephemeral;

internal sealed class ReusableMongoRunnerProvider : IDisposable
{
    private static readonly ConcurrentDictionary<string, Lazy<ReusableMongoRunner>> LazyRunners = new(StringComparer.Ordinal);

    private readonly Guid _renterId;

    public ReusableMongoRunnerProvider()
    {
        this._renterId = Guid.NewGuid();
    }

    public ReusableMongoRunner GetRunner(string clientName)
    {
        var runner = LazyRunners.GetOrAdd(clientName, CreateLazyReusableMongoRunner).Value;
        runner.Rent(this._renterId);
        return runner;
    }

    private static Lazy<ReusableMongoRunner> CreateLazyReusableMongoRunner(string clientName)
    {
        return new Lazy<ReusableMongoRunner>(static () => new ReusableMongoRunner());
    }

    public void Dispose()
    {
        foreach (var lazyRunner in LazyRunners.Values)
        {
            if (lazyRunner.IsValueCreated)
            {
                lazyRunner.Value.Return(this._renterId);
            }
        }
    }
}