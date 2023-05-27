using System.Text;
using GSoft.Extensions.Xunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using MongoDB.Driver.Core.Events;

namespace GSoft.Extensions.Mongo.Tests;

public class EventSubscriberTests : BaseIntegrationTest<EventSubscriberTests.EventSubscriberFixture>
{
    public EventSubscriberTests(EventSubscriberFixture fixture, ITestOutputHelper testOutputHelper)
        : base(fixture, testOutputHelper)
    {
    }

    [Fact]
    public async Task EventSubscribers_Are_Executed_In_The_Right_Order()
    {
        _ = await this.Services.GetRequiredService<IMongoCollection<DummyDocument>>().FindAsync(Builders<DummyDocument>.Filter.Empty);
        var actualOutput = this.Services.GetRequiredService<StringBuilder>().ToString();
        this.Logger.LogInformation("Event subscribers call hierarchy: \r\n{Ouput}", actualOutput);

        const string expectedOutput = @"
CommandTracingEventSubscriber.CommandStartedEvent(find)
  ApplicationInsightsEventSubscriber.CommandStartedEvent(find)
    CommandLoggingEventSubscriber.CommandStartedEvent(find)
    CommandLoggingEventSubscriber.CommandSucceededEvent(find)
  ApplicationInsightsEventSubscriber.CommandSucceededEvent(find)
CommandTracingEventSubscriber.CommandSucceededEvent(find)";

        Assert.Equal(expectedOutput.Trim(), actualOutput.Trim());
    }

    public class EventSubscriberFixture : MongoFixture
    {
        public override IServiceCollection ConfigureServices(IServiceCollection services)
        {
            base.ConfigureServices(services);

            var actualOutput = new StringBuilder();

            services.AddSingleton(actualOutput);
            services.Configure<MongoClientOptions>(options =>
            {
                var originalPostConfigureEventSubscribers = options.PostConfigureEventSubscribers;

                options.PostConfigureEventSubscribers = eventSubscribers =>
                {
                    originalPostConfigureEventSubscribers?.Invoke(eventSubscribers);

                    for (var i = 0; i < eventSubscribers.Count; i++)
                    {
                        eventSubscribers[i] = new StringBuilderOutputEventSubscriber(eventSubscribers[i], actualOutput, i);
                    }
                };
            });

            return services;
        }
    }

    private sealed class StringBuilderOutputEventSubscriber : IEventSubscriber
    {
        private static readonly object LockObj = new object();

        private readonly IEventSubscriber _eventSubscriber;
        private readonly StringBuilder _output;
        private readonly int _indentLevel;

        public StringBuilderOutputEventSubscriber(IEventSubscriber eventSubscriber, StringBuilder output, int indentLevel)
        {
            this._eventSubscriber = eventSubscriber;
            this._output = output;
            this._indentLevel = indentLevel;
        }

        public bool TryGetEventHandler<TEvent>(out Action<TEvent> handler)
        {
            if (this._eventSubscriber.TryGetEventHandler(out handler))
            {
                var originalHandler = handler;

                handler = evt =>
                {
                    var indent = new string(' ', this._indentLevel * 2);

                    var commandName = evt switch
                    {
                        CommandStartedEvent x => x.CommandName,
                        CommandSucceededEvent x => x.CommandName,
                        CommandFailedEvent x => x.CommandName,
                        _ => string.Empty,
                    };

                    if (!MongoTelemetryOptions.DefaultIgnoredCommandNames.Contains(commandName))
                    {
                        lock (LockObj)
                        {
                            this._output.AppendLine($"{indent}{this._eventSubscriber.GetType().Name}.{typeof(TEvent).Name}({commandName})");
                        }
                    }

                    originalHandler(evt);
                };
            }

            return handler != null;
        }
    }

    [MongoCollection("dummy")]
    private class DummyDocument : MongoDocument
    {
    }
}