using MongoDB.Driver.Core.Events;

namespace ShareGate.Infra.Mongo;

public interface IMongoEventSubscriberFactory
{
    IEnumerable<IEventSubscriber> CreateEventSubscribers(string clientName);
}