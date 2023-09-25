using System.Text;
using Workleap.Extensions.Xunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;

namespace Workleap.Extensions.Mongo.Tests;

[Collection(XunitCollectionConstants.PerformanceCritical)]
public sealed class CommandPerformanceTests : BaseIntegrationTest<CommandPerformanceTests.CommandPerformanceFixture>
{
    public CommandPerformanceTests(CommandPerformanceFixture fixture, ITestOutputHelper testOutputHelper)
        : base(fixture, testOutputHelper)
    {
    }

    [Fact]
    public async Task CollectionScanDetection_Works()
    {
        // Sink where all the logs are collected
        var logger = this.Services.GetRequiredService<InMemoryLogger>();

        // Prepare a collection with multiple items with one field "Name" which is not indexed
        var dogsCollection = this.Services.GetRequiredService<IMongoCollection<DogDocument>>();

        await dogsCollection.InsertOneAsync(new DogDocument { Id = "1", Name = "Foo" });
        await dogsCollection.InsertOneAsync(new DogDocument { Id = "2", Name = "Bar" });
        await dogsCollection.InsertOneAsync(new DogDocument { Id = "3", Name = "Qux" });

        // 1) Find a document by ID, this should NOT trigger a collection scan
        // Note that performance analysis is executed in background so we need to wait a little bit to ensure its completion
        _ = await dogsCollection.Find(x => x.Id == "2").ToListAsync();
        await Task.Delay(TimeSpan.FromMilliseconds(500));
        Assert.DoesNotContain("Collection scan detected on command", logger.ToString());

        // 2) Enumerate the collection, this should trigger a collection scan
        _ = await dogsCollection.Find(FilterDefinition<DogDocument>.Empty).ToListAsync();
        await Task.Delay(TimeSpan.FromMilliseconds(500));
        Assert.Contains("Collection scan detected on command", logger.ToString());
    }

    public sealed class CommandPerformanceFixture : MongoFixture
    {
        public override IServiceCollection ConfigureServices(IServiceCollection services)
        {
            base.ConfigureServices(services);

            services.AddMongo(options =>
            {
                options.CommandPerformanceAnalysis.EnableCollectionScanDetection = true;
            });

            var inMemoryLogger = new InMemoryLogger();
            services.AddSingleton(inMemoryLogger);

            var inMemoryLoggerProvider = new InMemoryLoggerProvider(inMemoryLogger);
            services.AddLogging(x => x.AddProvider(inMemoryLoggerProvider));

            return services;
        }
    }

    [MongoCollection("dogs")]
    private sealed class DogDocument : MongoDocument
    {
        public string Name { get; set; } = string.Empty;
    }

    private sealed class InMemoryLoggerProvider : ILoggerProvider
    {
        private readonly InMemoryLogger _logger;

        public InMemoryLoggerProvider(InMemoryLogger logger)
        {
            this._logger = logger;
        }

        public void Dispose()
        {
        }

        public ILogger CreateLogger(string categoryName)
        {
            return this._logger;
        }
    }

    private sealed class InMemoryLogger : ILogger
    {
        private readonly StringBuilder _sb;

        public InMemoryLogger()
        {
            this._sb = new StringBuilder();
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            this._sb.AppendLine(formatter(state, exception));
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public IDisposable BeginScope<TState>(TState state)
        {
            return NoopDisposable.Instance;
        }

        public override string ToString()
        {
            return this._sb.ToString();
        }
    }
}