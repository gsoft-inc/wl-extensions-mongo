using MongoDB.Driver.Core.Events;

namespace GSoft.Extensions.Mongo;

public interface IMongoEventSubscriberFactory
{
    IEnumerable<IEventSubscriber> CreateEventSubscribers(string clientName);
}