using System.Diagnostics.CodeAnalysis;
using MongoDB.Driver.Core.Events;

namespace GSoft.Extensions.Mongo;

public class AggregatorEventSubscriber : IEventSubscriber
{
    private readonly List<IEventSubscriber> _subscribers;

    public AggregatorEventSubscriber()
    {
        this._subscribers = new List<IEventSubscriber>();
    }

    public void Subscribe<TEvent>(Action<TEvent> handler)
    {
        if (handler == null)
        {
            throw new ArgumentNullException(nameof(handler));
        }

        this.Subscribe(new SingleEventSubscriber<TEvent>(handler));
    }

    public void Subscribe(IEventSubscriber subscriber)
    {
        if (subscriber == null)
        {
            throw new ArgumentNullException(nameof(subscriber));
        }

        this._subscribers.Add(subscriber);
    }

    public bool TryGetEventHandler<TEvent>([MaybeNullWhen(false)] out Action<TEvent> handler)
    {
        handler = null;

        foreach (var subscriber in this._subscribers)
        {
            if (subscriber.TryGetEventHandler<TEvent>(out var handlerLink))
            {
                if (handler == null)
                {
                    handler = handlerLink;
                }
                else
                {
                    handler += handlerLink;
                }
            }
        }

        return handler != null;
    }
}