using System.Diagnostics;
using Workleap.Extensions.Xunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using OpenTelemetry;
using OpenTelemetry.Trace;

namespace Workleap.Extensions.Mongo.Tests;

public sealed class DistributedTracingTests : BaseIntegrationTest<DistributedTracingTests.DistributedTracingFixture>, IAsyncLifetime
{
    public DistributedTracingTests(DistributedTracingFixture fixture, ITestOutputHelper testOutputHelper)
        : base(fixture, testOutputHelper)
    {
    }

    public async Task InitializeAsync()
    {
        // Required to start OpenTelemetry collector the same way it would have done it in a real IHost application
        await Task.WhenAll(this.Services.GetServices<IHostedService>().Select(x => x.StartAsync(CancellationToken.None)));
    }

    public async Task DisposeAsync()
    {
        await Task.WhenAll(this.Services.GetServices<IHostedService>().Select(x => x.StopAsync(CancellationToken.None)));
    }

    [Fact]
    public async Task Distributed_Tracing_On_Mongo_Commands_Works()
    {
        var recorder = this.Services.GetRequiredService<InMemoryActivityRecorder>();
        var things = this.Services.GetRequiredService<IMongoCollection<ThingDocument>>();

        await things.InsertOneAsync(new ThingDocument());

        // Distributed tracing records activities from all tests running concurrently so we might end up capturing activities of other tests
        var activities = recorder.RecordedActivities.ToArray();
        Assert.NotEmpty(activities);
        var activity = Assert.Single(activities, x => x.DisplayName == "things.insert");

        Assert.Equal("MongoDB.Driver.Core.Events.Command", activity.OperationName);
        Assert.Equal(ActivityStatusCode.Ok, activity.Status);

        var tags = activity.Tags.ToDictionary(k => k.Key, x => x.Value);

        AssertKeyValuePair(tags, "db.system", "mongodb");
        AssertKeyValuePair(tags, "db.mongodb.collection", "things");
        AssertKeyValuePair(tags, "db.operation", "insert");
        AssertKeyValuePair(tags, "otel.status_code", "OK");
    }

    private static void AssertKeyValuePair(IDictionary<string, string?> dictionary, string key, string? expectedValue)
    {
        var actualValue = Assert.Contains(key, dictionary);
        Assert.Equal(expectedValue, actualValue);
    }

    [MongoCollection("things")]
    private sealed class ThingDocument : MongoDocument
    {
    }

    public sealed class DistributedTracingFixture : MongoFixture
    {
        public override IServiceCollection ConfigureServices(IServiceCollection services)
        {
            base.ConfigureServices(services);

            services.AddOpenTelemetry().WithTracing(builder =>
            {
                builder.AddSource("Workleap.Extensions.Mongo");
                builder.AddProcessor<InMemoryActivityRecorder>();
            });

            return services;
        }
    }

    private sealed class InMemoryActivityRecorder : BaseProcessor<Activity>
    {
        private readonly List<Activity> _activities;

        public InMemoryActivityRecorder()
        {
            this._activities = new List<Activity>();
        }

        public IEnumerable<Activity> RecordedActivities
        {
            get
            {
                lock (this._activities)
                {
                    return new List<Activity>(this._activities);
                }
            }
        }

        public override void OnEnd(Activity data)
        {
            lock (this._activities)
            {
                this._activities.Add(data);
            }
        }
    }
}