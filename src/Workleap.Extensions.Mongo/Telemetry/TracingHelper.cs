using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace Workleap.Extensions.Mongo.Telemetry;

internal static class TracingHelper
{
    private const string ActivityName = "MongoDB.Driver.Core.Events.Command";

    private static readonly AssemblyName AssemblyName = typeof(CommandTracingEventSubscriber).Assembly.GetName();
    private static readonly string ActivitySourceName = AssemblyName.Name!;
    private static readonly Version Version = AssemblyName.Version!;
    private static readonly ActivitySource ActivitySource = new ActivitySource(ActivitySourceName, Version.ToString());

    [SuppressMessage("ReSharper", "ExplicitCallerInfoArgument", Justification = "We want a specific activity name, not the caller method name")]
    public static Activity? StartActivity()
    {
        return ActivitySource.StartActivity(ActivityName, ActivityKind.Client);
    }

    public static bool IsMongoActivity(Activity activity) => ReferenceEquals(ActivitySource, activity.Source);

    // Seems like MongoDB async commands are not sticking to the Activity.Current flow and we need this workaround:
    // https://github.com/jbogard/MongoDB.Driver.Core.Extensions.DiagnosticSources/pull/5
    public static void WithTemporaryCurrentActivity<TEvent, TDep>(Activity? newTemporaryActivity, TEvent evt, TDep dependency, Action<TDep, TEvent> action)
        where TEvent : struct
    {
        var existingActivity = Activity.Current;

        if (existingActivity == newTemporaryActivity)
        {
            action(dependency, evt);
            return;
        }

        try
        {
            Activity.Current = newTemporaryActivity;
            action(dependency, evt);
        }
        finally
        {
            Activity.Current = existingActivity;
        }
    }

    public static void AddSpanEventWithTags(string spanEventName, IEnumerable<KeyValuePair<string, object?>> tags)
    {
        Activity.Current?.AddEvent(new ActivityEvent(spanEventName, tags: new ActivityTagsCollection(tags)));
    }
}