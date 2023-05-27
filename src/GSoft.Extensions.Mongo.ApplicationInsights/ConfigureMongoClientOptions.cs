using GSoft.Extensions.Mongo.Telemetry;
using Microsoft.Extensions.Options;

namespace GSoft.Extensions.Mongo.ApplicationInsights;

internal sealed class ConfigureMongoClientOptions : IConfigureNamedOptions<MongoClientOptions>
{
    public void Configure(MongoClientOptions options)
    {
    }

    public void Configure(string name, MongoClientOptions options)
    {
        var existingPostConfigureEventSubscribers = options.PostConfigureEventSubscribers;

        options.PostConfigureEventSubscribers = eventSubscribers =>
        {
            var openTelemetryEventSubscriberIdx = eventSubscribers.FindIndex(x => x is CommandTracingEventSubscriber);
            var appInsightsEventSubscriberIdx = eventSubscribers.FindIndex(x => x is ApplicationInsightsEventSubscriber);

            if (openTelemetryEventSubscriberIdx != -1 && appInsightsEventSubscriberIdx != -1)
            {
                // Make sure the Application Insights event subscriber is after the OpenTelemetry event subscriber
                var appInsightsEventSubscriber = eventSubscribers[appInsightsEventSubscriberIdx];
                eventSubscribers.RemoveAt(appInsightsEventSubscriberIdx);
                eventSubscribers.Insert(openTelemetryEventSubscriberIdx + 1, appInsightsEventSubscriber);
            }

            existingPostConfigureEventSubscribers?.Invoke(eventSubscribers);
        };
    }
}