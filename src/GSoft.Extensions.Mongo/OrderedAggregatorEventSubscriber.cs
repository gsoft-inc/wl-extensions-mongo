using System.Diagnostics.CodeAnalysis;
using MongoDB.Driver.Core.Events;

namespace GSoft.Extensions.Mongo;

/// <summary>
/// Aggregate event subscriber that ensures that "finishing" event handlers are executed
/// in the reverse order than their "starting" event handlers counterparts.
/// <code>
/// Given the following example of four event subscribers that handle both command "started" and command "succeeded" events:
/// - OpenTelemetry, ApplicationInsights, Logging and UserDefined
///
/// By defaut the order of execution of the event handlers would be:
///
/// 1. OpenTelemetry.CommandStarted
///   2. ApplicationInsights.CommandStarted
///     3. Logging.CommandStarted
///       4. UserDefined.CommandStarted
/// 1. OpenTelemetry.CommandSucceeded
///   2. ApplicationInsights.CommandSucceeded
///     3. Logging.CommandSucceeded
///       4. UserDefined.CommandSucceeded
///
/// This is incorrect! We want begin/end event handlers to act as nested "middlewares", especially when an event subscriber depends on inner ones.
/// This class makes sure that the order of execution is:
///
/// 1. OpenTelemetry.CommandStarted
///   2. ApplicationInsights.CommandStarted
///     3. Logging.CommandStarted
///       4. UserDefined.CommandStarted
///       4. UserDefined.CommandSucceeded
///     3. Logging.CommandSucceeded
///   2. ApplicationInsights.CommandSucceeded
/// 1. OpenTelemetry.CommandSucceeded
///
/// In this example, the benefits are:
///   - Any exception or third-party interaction inside user defined ended event handlers will be instrumented by our event handlers.
///   - The recorded command ended log will be part of the application insights operation and open telemetry activity as events.
///   - The application insights operation won't be discarded, because its parent open telemetry activity will still be active.
///   - The open telemetry activity is stopped after every other event handler has been executed.
/// </code>
/// </summary>
internal sealed class OrderedAggregatorEventSubscriber : IEventSubscriber
{
    // The list of "finishing" events that have a "starting" counterpart.
    // See: https://mongodb.github.io/mongo-csharp-driver/2.9/apidocs/html/N_MongoDB_Driver_Core_Events.htm
    private static readonly HashSet<Type> FinishingEventTypes = new HashSet<Type>
    {
        typeof(CommandSucceededEvent), // CommandStartedEvent
        typeof(CommandFailedEvent), // CommandStartedEvent
        typeof(ClusterAddedServerEvent), // ClusterAddingServerEvent
        typeof(ClusterClosedEvent), // ClusterClosingEvent
        typeof(ClusterOpenedEvent), // ClusterOpeningEvent
        typeof(ClusterRemovedServerEvent), // ClusterRemovingServerEvent
        typeof(ClusterSelectedServerEvent), // ClusterSelectingServerEvent
        typeof(ClusterSelectingServerFailedEvent), // ClusterSelectingServerEvent
        typeof(ConnectionClosedEvent), // ConnectionClosingEvent
        typeof(ConnectionOpenedEvent), // ConnectionOpeningEvent
        typeof(ConnectionOpeningFailedEvent), // ConnectionOpeningEvent
        typeof(ConnectionPoolAddedConnectionEvent), // ConnectionPoolAddingConnectionEvent
        typeof(ConnectionPoolCheckedInConnectionEvent), // ConnectionPoolCheckingInConnectionEvent
        typeof(ConnectionPoolCheckedOutConnectionEvent), // ConnectionPoolCheckingOutConnectionEvent
        typeof(ConnectionPoolCheckingOutConnectionFailedEvent), // ConnectionPoolCheckingOutConnectionEvent
        typeof(ConnectionPoolClosedEvent), // ConnectionPoolClosingEvent
        typeof(ConnectionPoolOpenedEvent), // ConnectionPoolOpeningEvent
        typeof(ConnectionPoolRemovedConnectionEvent), // ConnectionPoolRemovingConnectionEvent
        typeof(ConnectionReceivedMessageEvent), // ConnectionReceivingMessageEvent
        typeof(ConnectionReceivingMessageFailedEvent), // ConnectionReceivingMessageEvent
        typeof(ConnectionSentMessagesEvent), // ConnectionSendingMessagesEvent
        typeof(ConnectionSendingMessagesFailedEvent), // ConnectionSendingMessagesEvent
        typeof(ServerClosedEvent), // ServerClosingEvent
        typeof(ServerHeartbeatSucceededEvent), // ServerHeartbeatStartedEvent
        typeof(ServerHeartbeatFailedEvent), // ServerHeartbeatStartedEvent
        typeof(ServerOpenedEvent), // ServerOpeningEvent
    };

    private readonly IEventSubscriber[] _subscribers;

    public OrderedAggregatorEventSubscriber(IEnumerable<IEventSubscriber> subscribers)
    {
        this._subscribers = subscribers.ToArray();
    }

    public bool TryGetEventHandler<TEvent>([MaybeNullWhen(false)] out Action<TEvent> handler)
    {
        handler = this.GetOrderedHandlers<TEvent>();
        return handler != null;
    }

    private Action<TEvent>? GetOrderedHandlers<TEvent>()
    {
        // MongoDB C# driver calls this method only once per event type and caches it internally,
        // so there's no need to overly optimize this process or cache the result.
        var subscribers = FinishingEventTypes.Contains(typeof(TEvent)) ? this._subscribers.Reverse() : this._subscribers;

        Action<TEvent>? handler = null;
        return subscribers.Aggregate(handler, ChainHandler);
    }

    private static Action<TEvent>? ChainHandler<TEvent>(Action<TEvent>? handler, IEventSubscriber subscriber)
    {
        if (subscriber.TryGetEventHandler<TEvent>(out var handlerLink))
        {
            return handler == null ? handlerLink : handler + handlerLink;
        }

        return handler;
    }
}