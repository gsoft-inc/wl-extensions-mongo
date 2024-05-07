using System.Diagnostics;
using System.Net;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Core.Clusters;
using MongoDB.Driver.Core.Connections;
using MongoDB.Driver.Core.Events;
using MongoDB.Driver.Core.Servers;
using Workleap.Extensions.Mongo.Telemetry;

namespace Workleap.Extensions.Mongo.Tests
{
    public class DiagnosticsEventSubscriberTests
    {
        static DiagnosticsEventSubscriberTests()
        {
            Activity.DefaultIdFormat = ActivityIdFormat.W3C;
            Activity.ForceDefaultIdFormat = true;
        }

        [Fact]
        public void Should_Record_Diagnostics_Events_When_Option_Set()
        {
            var stopFired = false;
            var startFired = false;

            var command = new BsonDocument(new Dictionary<string, object>
            {
                { "update", "my_collection" },
            });

            using var listener = new ActivityListener
            {
                ShouldListenTo = source => source.Name == "Workleap.Extensions.Mongo",
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
                ActivityStarted = activity =>
                {
                    startFired = true;
                    Assert.NotNull(activity);
                },
                ActivityStopped = activity =>
                {
                    Assert.NotNull(activity);
                    Assert.Equal("MongoDB.Driver.Core.Events.Command", activity.OperationName);
                    var activityEvents = activity.Events;
                    Assert.Single(activityEvents);
                    var connPoolAddedConnEvent = activityEvents.First();
                    Assert.Equal("ConnectionPoolAddedConnectionEvent", connPoolAddedConnEvent.Name);
                    var operationId = connPoolAddedConnEvent.Tags.SingleOrDefault(t => t.Key == "db.mongodb.OperationId");                   
                    Assert.Equal(Convert.ToInt64(1), operationId.Value);

                    stopFired = true;
                },
            };
            ActivitySource.AddActivityListener(listener);

            var options = new MongoClientOptions();
            options.Telemetry.CaptureDiagnosticEvents = true;

            var tracingBehavior = new CommandTracingEventSubscriber(options);
            var diagnosticsBehavior = new DiagnosticsTracingEventSubscriber(options);

            Assert.True(tracingBehavior.TryGetEventHandler<CommandStartedEvent>(out var startEvent));
            Assert.True(diagnosticsBehavior.TryGetEventHandler<ConnectionPoolAddedConnectionEvent>(out var connPoolAddedConnEvent));
            Assert.True(tracingBehavior.TryGetEventHandler<CommandSucceededEvent>(out var stopEvent));

            var connectionId = new ConnectionId(new ServerId(new ClusterId(), new DnsEndPoint("localhost", 8000)));
            var databaseNamespace = new DatabaseNamespace("test");
            startEvent(new CommandStartedEvent("update", command, databaseNamespace, null, 1, connectionId));
            connPoolAddedConnEvent(new ConnectionPoolAddedConnectionEvent(connectionId, TimeSpan.Zero, 1));
            stopEvent(new CommandSucceededEvent("update", command, databaseNamespace, null, 1, connectionId, TimeSpan.Zero));

            Assert.True(startFired);
            Assert.True(stopFired);
        }
    }
}
