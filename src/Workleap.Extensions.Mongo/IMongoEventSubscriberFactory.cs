using MongoDB.Driver.Core.Events;

namespace Workleap.Extensions.Mongo;

public interface IMongoEventSubscriberFactory
{
    IEnumerable<IEventSubscriber> CreateEventSubscribers(string clientName);
}