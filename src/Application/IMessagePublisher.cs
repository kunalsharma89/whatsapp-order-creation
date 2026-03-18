using Shared.Contracts;

namespace Application;

public interface IMessagePublisher
{
    Task PublishOrderReceivedAsync(OrderReceivedEvent evt, CancellationToken cancellationToken = default);
    Task PublishOrderProcessedAsync(OrderProcessedEvent evt, CancellationToken cancellationToken = default);
}
