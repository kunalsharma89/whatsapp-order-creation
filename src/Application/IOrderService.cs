using Shared.Contracts;

namespace Application;

public interface IOrderService
{
    /// <summary>
    /// Creates an order from the received event. Idempotent by ExternalMessageId (MessageId).
    /// </summary>
    Task<CreateOrderResult> CreateOrderAsync(OrderReceivedEvent receivedEvent, CancellationToken cancellationToken = default);
}
