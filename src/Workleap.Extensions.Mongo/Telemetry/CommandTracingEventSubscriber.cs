﻿using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using MongoDB.Driver.Core.Events;

namespace Workleap.Extensions.Mongo.Telemetry;

// Highly inspired from Jimmy Bogard's MongoDB instrumentation library:
// https://github.com/jbogard/MongoDB.Driver.Core.Extensions.DiagnosticSources/blob/1.3.0/src/MongoDB.Driver.Core.Extensions.DiagnosticSources/DiagnosticsActivityEventSubscriber.cs
internal sealed class CommandTracingEventSubscriber : AggregatorEventSubscriber
{
    private readonly MongoClientOptions _options;
    private readonly ConcurrentDictionary<int, Activity> _currentActivitiesMap;

    public CommandTracingEventSubscriber(MongoClientOptions options)
    {
        this._options = options;
        this._currentActivitiesMap = new ConcurrentDictionary<int, Activity>();

        this.Subscribe<CommandStartedEvent>(this.CommandStartedEventHandler);
        this.Subscribe<CommandSucceededEvent>(this.CommandSucceededEventHandler);
        this.Subscribe<CommandFailedEvent>(this.CommandFailedEventHandler);
    }

    [SuppressMessage("ReSharper", "ExplicitCallerInfoArgument", Justification = "We want a specific activity name, not the caller method name")]
    private void CommandStartedEventHandler(CommandStartedEvent evt)
    {
        if (this._options.Telemetry.IgnoredCommandNames.Contains(evt.CommandName))
        {
            return;
        }

        var activity = TracingHelper.StartActivity();
        if (activity == null)
        {
            return;
        }

        this.StartActivityOnCommandStarted(evt, activity);
    }

    private void StartActivityOnCommandStarted(CommandStartedEvent evt, Activity activity)
    {
        var collectionName = evt.GetCollectionName();

        // We try to be compliant with OpenTelemetry database semantic conventions:
        // https://github.com/open-telemetry/opentelemetry-specification/blob/master/specification/trace/semantic_conventions/database.md
        activity.DisplayName = $"{collectionName ?? "mongodb"}.{evt.CommandName}";

        activity.AddTag("db.system", "mongodb");
        activity.AddTag("db.connection_id", evt.ConnectionId?.ToString());
        activity.AddTag("db.name", evt.DatabaseNamespace?.DatabaseName);
        activity.AddTag("db.mongodb.collection", collectionName);
        activity.AddTag("db.operation", evt.CommandName);
        activity.AddTag("net.transport", "ip_tcp");

        var endPoint = evt.ConnectionId?.ServerId?.EndPoint;
        switch (endPoint)
        {
            case IPEndPoint ipEndPoint:
                activity.AddTag("net.peer.port", ipEndPoint.Port.ToString());
                activity.AddTag("net.sock.peer.addr", ipEndPoint.Address.ToString());
                break;
            case DnsEndPoint dnsEndPoint:
                activity.AddTag("net.peer.name", dnsEndPoint.Host);
                activity.AddTag("net.peer.port", dnsEndPoint.Port.ToString());
                break;
        }

        if (activity.IsAllDataRequested && this._options.Telemetry.CaptureCommandText)
        {
            activity.AddTag("db.statement", evt.Command.ToString());
        }

        this._currentActivitiesMap.TryAdd(evt.RequestId, activity);
    }

    private void CommandSucceededEventHandler(CommandSucceededEvent evt)
    {
        if (this._currentActivitiesMap.TryRemove(evt.RequestId, out var activity))
        {
            TracingHelper.WithTemporaryCurrentActivity(activity, evt, activity, EndActivityOnCommandSucceeded);
        }
    }

    private static void EndActivityOnCommandSucceeded(Activity activity, CommandSucceededEvent evt)
    {
        activity.SetEndTime(evt.Timestamp);
        activity.AddTag("otel.status_code", "OK");
        activity.SetStatus(ActivityStatusCode.Ok);
        activity.Dispose();
    }

    private void CommandFailedEventHandler(CommandFailedEvent evt)
    {
        if (this._currentActivitiesMap.TryRemove(evt.RequestId, out var activity))
        {
            TracingHelper.WithTemporaryCurrentActivity(activity, evt, activity, EndActivityOnCommandFailed);
        }
    }

    private static void EndActivityOnCommandFailed(Activity activity, CommandFailedEvent evt)
    {
        if (activity.IsAllDataRequested)
        {
            activity.AddTag("otel.status_code", "ERROR");
            activity.AddTag("otel.status_description", evt.Failure.Message);
            activity.AddTag("exception.type", evt.Failure.GetType().FullName);
            activity.AddTag("exception.message", evt.Failure.Message);
            activity.AddTag("exception.stacktrace", evt.Failure.StackTrace);
        }

        activity.SetEndTime(evt.Timestamp);
        activity.SetStatus(ActivityStatusCode.Error);
        activity.Dispose();
    }
}