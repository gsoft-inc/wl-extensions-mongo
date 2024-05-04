using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using MongoDB.Driver.Core.Events;

namespace Workleap.Extensions.Mongo.Telemetry;

// Highly inspired from Jimmy Bogard's MongoDB instrumentation library:
// https://github.com/jbogard/MongoDB.Driver.Core.Extensions.DiagnosticSources/blob/1.3.0/src/MongoDB.Driver.Core.Extensions.DiagnosticSources/DiagnosticsActivityEventSubscriber.cs
internal sealed class DiagnosticsTracingEventSubscriber : IEventSubscriber
{
    private const string MongoDbPrefix = "db.mongodb";
    private readonly MongoClientOptions _options;
    private readonly ReflectionEventSubscriber _subscriber;
    private readonly ConcurrentDictionary<Type, PropertyInfo[]> _propertyInfoCache = new();

    public DiagnosticsTracingEventSubscriber(MongoClientOptions options)
    {
        this._options = options;
        this._subscriber = new ReflectionEventSubscriber(this, bindingFlags: BindingFlags.Instance | BindingFlags.NonPublic);
    }

    public bool TryGetEventHandler<TEvent>(out Action<TEvent> handler)
            => this._subscriber.TryGetEventHandler(out handler);

    #region cluster events
    private void Handle(ClusterAddedServerEvent @event) => this.HandleDiagnosticEvent(@event);

    private void Handle(ClusterAddingServerEvent @event) => this.HandleDiagnosticEvent(@event);

    private void Handle(ClusterClosedEvent @event) => this.HandleDiagnosticEvent(@event);

    private void Handle(ClusterClosingEvent @event) => this.HandleDiagnosticEvent(@event);

    private void Handle(ClusterDescriptionChangedEvent @event) => this.HandleDiagnosticEvent(@event);

    private void Handle(ClusterOpenedEvent @event) => this.HandleDiagnosticEvent(@event);

    private void Handle(ClusterOpeningEvent @event) => this.HandleDiagnosticEvent(@event);

    private void Handle(ClusterRemovedServerEvent @event) => this.HandleDiagnosticEvent(@event);

    private void Handle(ClusterRemovingServerEvent @event) => this.HandleDiagnosticEvent(@event);

    private void Handle(ClusterSelectedServerEvent @event) => this.HandleDiagnosticEvent(@event);

    private void Handle(ClusterSelectingServerEvent @event) => this.HandleDiagnosticEvent(@event);

    private void Handle(ClusterSelectingServerFailedEvent @event) => this.HandleDiagnosticEvent(@event);
    #endregion

    #region connection events
    private void Handle(ConnectionClosedEvent @event) => this.HandleDiagnosticEvent(@event);

    private void Handle(ConnectionClosingEvent @event) => this.HandleDiagnosticEvent(@event);

    private void Handle(ConnectionCreatedEvent @event) => this.HandleDiagnosticEvent(@event);

    private void Handle(ConnectionFailedEvent @event) => this.HandleDiagnosticEvent(@event);

    private void Handle(ConnectionOpenedEvent @event) => this.HandleDiagnosticEvent(@event);

    private void Handle(ConnectionOpeningEvent @event) => this.HandleDiagnosticEvent(@event);

    private void Handle(ConnectionOpeningFailedEvent @event) => this.HandleDiagnosticEvent(@event);

    private void Handle(ConnectionReceivedMessageEvent @event) => this.HandleDiagnosticEvent(@event);

    private void Handle(ConnectionReceivingMessageEvent @event) => this.HandleDiagnosticEvent(@event);

    private void Handle(ConnectionReceivingMessageFailedEvent @event) => this.HandleDiagnosticEvent(@event);

    private void Handle(ConnectionSendingMessagesEvent @event) => this.HandleDiagnosticEvent(@event);

    private void Handle(ConnectionSendingMessagesFailedEvent @event) => this.HandleDiagnosticEvent(@event);

    private void Handle(ConnectionSentMessagesEvent @event) => this.HandleDiagnosticEvent(@event);
    #endregion

    #region connection pool events
    private void Handle(ConnectionPoolAddedConnectionEvent @event) => this.HandleDiagnosticEvent(@event);

    private void Handle(ConnectionPoolAddingConnectionEvent @event) => this.HandleDiagnosticEvent(@event);

    private void Handle(ConnectionPoolCheckedInConnectionEvent @event) => this.HandleDiagnosticEvent(@event);

    private void Handle(ConnectionPoolCheckedOutConnectionEvent @event) => this.HandleDiagnosticEvent(@event);

    private void Handle(ConnectionPoolCheckingInConnectionEvent @event) => this.HandleDiagnosticEvent(@event);

    private void Handle(ConnectionPoolCheckingOutConnectionEvent @event) => this.HandleDiagnosticEvent(@event);

    private void Handle(ConnectionPoolCheckingOutConnectionFailedEvent @event) => this.HandleDiagnosticEvent(@event);

    private void Handle(ConnectionPoolClearedEvent @event) => this.HandleDiagnosticEvent(@event);

    private void Handle(ConnectionPoolClearingEvent @event) => this.HandleDiagnosticEvent(@event);

    private void Handle(ConnectionPoolClosedEvent @event) => this.HandleDiagnosticEvent(@event);

    private void Handle(ConnectionPoolClosingEvent @event) => this.HandleDiagnosticEvent(@event);

    private void Handle(ConnectionPoolOpenedEvent @event) => this.HandleDiagnosticEvent(@event);

    private void Handle(ConnectionPoolOpeningEvent @event) => this.HandleDiagnosticEvent(@event);

    private void Handle(ConnectionPoolReadyEvent @event) => this.HandleDiagnosticEvent(@event);

    private void Handle(ConnectionPoolRemovedConnectionEvent @event) => this.HandleDiagnosticEvent(@event);

    private void Handle(ConnectionPoolRemovingConnectionEvent @event) => this.HandleDiagnosticEvent(@event);
    #endregion

    #region server events
    private void Handle(ServerClosedEvent @event) => this.HandleDiagnosticEvent(@event);

    private void Handle(ServerClosingEvent @event) => this.HandleDiagnosticEvent(@event);

    private void Handle(ServerDescriptionChangedEvent @event) => this.HandleDiagnosticEvent(@event);

    private void Handle(ServerHeartbeatFailedEvent @event) => this.HandleDiagnosticEvent(@event);

    private void Handle(ServerHeartbeatStartedEvent @event) => this.HandleDiagnosticEvent(@event);

    private void Handle(ServerHeartbeatSucceededEvent @event) => this.HandleDiagnosticEvent(@event);

    private void Handle(ServerOpenedEvent @event) => this.HandleDiagnosticEvent(@event);

    private void Handle(ServerOpeningEvent @event) => this.HandleDiagnosticEvent(@event);
    #endregion

    private void HandleDiagnosticEvent(object diagnosticEvent)
    {
        Activity? currentActivity = Activity.Current;
        if (!this._options.Telemetry.CaptureDiagnosticEvents || currentActivity is null)
        {
            return;
        }

        Type t = diagnosticEvent.GetType();
        PropertyInfo[] props = this._propertyInfoCache.GetOrAdd(t, x => x.GetProperties(BindingFlags.Instance | BindingFlags.Public));

        var tags = new List<KeyValuePair<string, object?>>(props.Length);
        foreach (var prop in props)
        {
            var val = prop.GetValue(diagnosticEvent, null);
            val ??= string.Empty;

            tags.Add(new KeyValuePair<string, object?>($"{MongoDbPrefix}.{prop.Name}", val));
        }

        TracingHelper.AddSpanEventWithTags(currentActivity, t.Name, tags);
    }
}